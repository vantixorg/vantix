#[compute]
/*
 * License: Apache-2.0
 * Copyright 2026 Stefan Kalysta (stefan@redninjas.dev)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0) uniform sampler2D src_color;
layout(rgba16f, set = 0, binding = 1) uniform writeonly image2D dst_image;
layout(set = 0, binding = 2) uniform sampler2D depth_tex;
layout(set = 0, binding = 3) uniform sampler2D velocity_tex;
layout(set = 0, binding = 4, std430) buffer EyeAdapt {
	float adapted;
	float enable;
	float key;
	float min_exp;
	float max_exp;
	float speed_light;
	float speed_dark;
	float dt;
} adapt;

layout(push_constant, std430) uniform Params {
	mat4 inv_view_proj;
	vec3 camera_pos;
	float motion_blur;
	vec2 raster_size;
	float aberration;
	float sharpen;
	float grain_strength;
	float time;
	float mode;
	float vignette_strength;
	float vignette_radius;
	float purkinje_strength;
	float bands_coverage;
	float bands_softness;
} p;

shared float s_lum[256];

vec3 load_uv(vec2 uv) {
	return texture(src_color, uv).rgb;
}

float hash12(vec2 v) {
	return fract(sin(dot(v, vec2(127.1, 311.7))) * 43758.5453);
}

float ign(vec2 q) {
	return fract(52.9829189 * fract(dot(q, vec2(0.06711056, 0.00583715))));
}

float vnoise(vec2 v) {
	vec2 i = floor(v);
	vec2 f = fract(v);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x),
		mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), f.x),
		f.y);
}

vec3 film_grain(vec3 col, vec2 px_coord, float t, float strength) {
	float gt = floor(t * 14.0);
	float gtf = fract(t * 14.0);
	vec2 sf = px_coord / 1.5;
	vec2 sc = px_coord / 3.5;
	vec3 n;
	for (int c = 0; c < 3; c++) {
		vec2 chOff = vec2(float(c) * 23.7, float(c) * 41.1);
		vec2 tOff0 = vec2(gt * 0.77, gt * 0.43);
		vec2 tOff1 = vec2((gt + 1.0) * 0.77, (gt + 1.0) * 0.43);
		float fine = mix(vnoise(sf + tOff0 + chOff), vnoise(sf + tOff1 + chOff), gtf);
		float coarse = mix(vnoise(sc + tOff0 * 0.5 + chOff), vnoise(sc + tOff1 * 0.5 + chOff), gtf);
		n[c] = (fine * 0.7 + coarse * 0.3) - 0.5;
	}
	float luma = dot(col, vec3(0.299, 0.587, 0.114));
	float w = 4.0 * luma * (1.0 - luma);
	w *= smoothstep(0.05, 0.18, luma);
	w *= 1.0 - smoothstep(0.85, 1.0, luma);
	return col + n * strength * w * 0.8;
}

void main() {
	ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
	int m = int(p.mode + 0.5);

	if (m == 2) {
		uint idx = gl_LocalInvocationIndex;
		vec2 guv = (vec2(gl_LocalInvocationID.xy) + 0.5) / 16.0;
		vec3 c = texture(src_color, guv).rgb;
		s_lum[idx] = clamp(dot(c, vec3(0.2126, 0.7152, 0.0722)), 0.0, 8.0);
		barrier();
		for (uint stride = 128u; stride > 0u; stride >>= 1u) {
			if (idx < stride) {
				s_lum[idx] += s_lum[idx + stride];
			}
			barrier();
		}
		if (idx == 0u) {
			float target = max(s_lum[0] / 256.0, 1e-3);
			float prev = adapt.adapted;
			float speed = (target > prev) ? adapt.speed_light : adapt.speed_dark;
			float k = 1.0 - exp(-adapt.dt * max(speed, 0.0));
			adapt.adapted = prev + (target - prev) * k;
		}
		return;
	}

	if (coord.x >= int(p.raster_size.x) || coord.y >= int(p.raster_size.y)) {
		return;
	}

	if (m == 0) {
		imageStore(dst_image, coord, texelFetch(src_color, coord, 0));
		return;
	}

	vec2 puv = (vec2(coord) + 0.5) / p.raster_size;
	vec4 src = texture(src_color, puv);
	vec2 uv = puv;

	vec2 cdir = uv - vec2(0.5);
	vec2 ca = cdir * length(cdir) * p.aberration;
	vec3 col;
	if (p.aberration > 0.0) {
		vec3 csum = vec3(0.0);
		vec3 wsum = vec3(0.0);
		const int CA_N = 5;
		for (int i = 0; i < CA_N; i++) {
			float t = float(i) / float(CA_N - 1);
			vec3 cw = vec3(
				clamp(1.0 - t * 2.0, 0.0, 1.0),
				1.0 - abs(t - 0.5) * 2.0,
				clamp(t * 2.0 - 1.0, 0.0, 1.0));
			csum += load_uv(uv + mix(ca, -ca, t)) * cw;
			wsum += cw;
		}
		col = csum / max(wsum, vec3(0.0001));
	} else {
		col = load_uv(uv);
	}

	if (p.motion_blur > 0.0) {
		vec2 vel = texture(velocity_tex, uv).rg * p.motion_blur;
		float vlen = length(vel);
		if (vlen > 0.12) {
			vel *= 0.12 / vlen;
		}
		if (vlen > 0.0008) {
			const int MB_TAPS = 8;
			float jitter = ign(vec2(coord)) - 0.5;
			vec3 acc = vec3(0.0);
			float msum = 0.0;
			for (int i = 0; i < MB_TAPS; i++) {
				float t = (float(i) + jitter) / float(MB_TAPS - 1) - 0.5;
				vec2 suv = uv + vel * t;
				float tap_v = length(texture(velocity_tex, suv).rg * p.motion_blur);
				float mw = mix(0.1, 1.0, clamp(tap_v / vlen, 0.0, 1.0));
				acc += load_uv(suv) * mw;
				msum += mw;
			}
			col = acc / max(msum, 0.0001);
		}
	}

	if (adapt.enable > 0.5) {
		col *= clamp(adapt.key / max(adapt.adapted, 1e-4), adapt.min_exp, adapt.max_exp);
	}

	if (p.sharpen > 0.0) {
		vec2 px = 1.0 / p.raster_size;
		vec3 blur4 = load_uv(uv + vec2(px.x, 0.0)) + load_uv(uv - vec2(px.x, 0.0))
			+ load_uv(uv + vec2(0.0, px.y)) + load_uv(uv - vec2(0.0, px.y));
		const vec3 LUMA = vec3(0.2126, 0.7152, 0.0722);
		float hp = dot(col - blur4 * 0.25, LUMA) * p.sharpen;
		col += vec3(clamp(hp, -0.5, 0.5));
	}

	float vig = 1.0 - smoothstep(p.vignette_radius * 0.4, p.vignette_radius, length(puv - vec2(0.5)));
	col *= mix(1.0, vig, p.vignette_strength);

	if (p.grain_strength > 0.0) {
		col = film_grain(col, vec2(coord) + 0.5, p.time, p.grain_strength);
	}

	if (p.purkinje_strength > 0.0) {
		float l = dot(col, vec3(0.2126, 0.7152, 0.0722));
		float dark = 1.0 - smoothstep(0.0, 0.2, l);
		vec3 scotopic = vec3(l) * vec3(0.7, 0.9, 1.4);
		col = mix(col, scotopic, dark * p.purkinje_strength);
	}

	if (p.bands_coverage > 0.0) {
		float bar = p.bands_coverage * 0.5;
		float s = max(p.bands_softness, 1e-4);
		col *= smoothstep(bar - s, bar + s, puv.y) * smoothstep(bar - s, bar + s, 1.0 - puv.y);
	}

	imageStore(dst_image, coord, vec4(max(col, vec3(0.0)), src.a));
}
