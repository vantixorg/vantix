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

layout(set = 0, binding = 0) uniform sampler2D src_a;
layout(rgba16f, set = 0, binding = 1) uniform restrict image2D dst_img;
layout(set = 0, binding = 2) uniform sampler2D src_b;
layout(set = 0, binding = 3) uniform sampler2D src_c;
layout(set = 0, binding = 4) uniform sampler2D src_d;

layout(push_constant, std430) uniform Params {
	vec2 raster_size;
	vec2 bright_size;
	float mode;
	float threshold;
	float intensity;
	float ghost_count;
	float dispersal;
	float chroma;
	float halo_width;
	float halo_strength;
	float streak_strength;
	float streak_length;
	float dirt_strength;
	float has_dirt;
	float bright_cap;
	float max_flare;
	float blur_radius;
	float starburst_strength;
	vec4 tint;
	vec4 streak_tint;
	float cross_strength;
	float cross_length;
	float _pad0;
	float _pad1;
} p;

const float PI = 3.14159265;

float luma(vec3 c) {
	return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

vec3 spectral(float t) {
	t = clamp(t, 0.0, 1.0);
	return clamp(vec3(1.5 - abs(4.0 * t - 3.0),
	                  1.5 - abs(4.0 * t - 2.0),
	                  1.5 - abs(4.0 * t - 1.0)), 0.0, 1.0);
}

vec3 disperse(vec2 base_uv, vec2 dir, float amount) {
	const int SPEC = 8;
	vec3 acc = vec3(0.0);
	vec3 wsum = vec3(0.0);
	for (int k = 0; k < SPEC; k++) {
		float t = (float(k) + 0.5) / float(SPEC);
		vec3 w = spectral(t);
		acc += texture(src_a, base_uv + dir * (t - 0.5) * 2.0 * amount).rgb * w;
		wsum += w;
	}
	return acc / max(wsum, vec3(1e-4));
}

vec3 disperse_b(vec2 base_uv, vec2 dir, float amount) {
	const int SPEC = 8;
	vec3 acc = vec3(0.0);
	vec3 wsum = vec3(0.0);
	for (int k = 0; k < SPEC; k++) {
		float t = (float(k) + 0.5) / float(SPEC);
		vec3 w = spectral(t);
		acc += texture(src_b, base_uv + dir * (t - 0.5) * 2.0 * amount).rgb * w;
		wsum += w;
	}
	return acc / max(wsum, vec3(1e-4));
}

float center_weight(vec2 uv, vec2 asp) {
	return pow(clamp(1.0 - length((uv - vec2(0.5)) * asp) / 0.7071, 0.0, 1.0), 5.0);
}

void main() {
	ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
	int m = int(p.mode + 0.5);
	ivec2 tgt = (m == 4) ? ivec2(p.raster_size) : ivec2(p.bright_size);
	if (coord.x >= tgt.x || coord.y >= tgt.y) {
		return;
	}
	vec2 uv = (vec2(coord) + 0.5) / vec2(tgt);
	float aspect = p.raster_size.x / max(p.raster_size.y, 1.0);
	vec2 asp = vec2(aspect, 1.0);

	if (m == 0) {
		vec2 tx = 1.0 / p.bright_size;
		vec3 A = texture(src_a, uv + tx * vec2(-1.0, -1.0)).rgb;
		vec3 B = texture(src_a, uv + tx * vec2( 0.0, -1.0)).rgb;
		vec3 C = texture(src_a, uv + tx * vec2( 1.0, -1.0)).rgb;
		vec3 D = texture(src_a, uv + tx * vec2(-1.0,  0.0)).rgb;
		vec3 E = texture(src_a, uv).rgb;
		vec3 F = texture(src_a, uv + tx * vec2( 1.0,  0.0)).rgb;
		vec3 G = texture(src_a, uv + tx * vec2(-1.0,  1.0)).rgb;
		vec3 H = texture(src_a, uv + tx * vec2( 0.0,  1.0)).rgb;
		vec3 I = texture(src_a, uv + tx * vec2( 1.0,  1.0)).rgb;
		vec3 J = texture(src_a, uv + tx * vec2(-0.5, -0.5)).rgb;
		vec3 K = texture(src_a, uv + tx * vec2( 0.5, -0.5)).rgb;
		vec3 L = texture(src_a, uv + tx * vec2(-0.5,  0.5)).rgb;
		vec3 M = texture(src_a, uv + tx * vec2( 0.5,  0.5)).rgb;
		vec3 c = (J + K + L + M) * 0.125
		       + (A + B + D + E) * 0.03125
		       + (B + C + E + F) * 0.03125
		       + (D + E + G + H) * 0.03125
		       + (E + F + H + I) * 0.03125;
		float l = luma(c);
		float t = max(l - p.threshold, 0.0);
		float soft = t * t / (t + 0.5);
		vec3 bright = c * (soft / max(l, 1e-4));
		float bl = luma(bright);
		if (bl > p.bright_cap) {
			bright *= p.bright_cap / bl;
		}
		imageStore(dst_img, coord, vec4(bright, 1.0));
		return;
	}

	if (m == 1) {
		const int B = 48;
		float radius = max(p.blur_radius, 0.0) / min(p.bright_size.x, p.bright_size.y);
		int blades = max(3, int(p.tint.w + 0.5));
		float bw = 6.2831853 / float(blades);
		vec3 acc = vec3(0.0);
		float wsum = 0.0;
		for (int i = 0; i < B; i++) {
			float a = float(i) * 2.3998277;
			float rn = sqrt((float(i) + 0.5) / float(B));
			float ap = cos(bw * 0.5) / cos(mod(a, bw) - bw * 0.5);
			float w = 1.0 - rn;
			acc += texture(src_a, uv + vec2(cos(a), sin(a)) * rn * radius * ap).rgb * w;
			wsum += w;
		}
		imageStore(dst_img, coord, vec4(acc / max(wsum, 1e-4), 1.0));
		return;
	}

	if (m == 2) {
		vec2 ghost_vec = (vec2(0.5) - uv) * p.dispersal;
		vec2 dir = normalize(ghost_vec + vec2(1e-6));
		vec3 result = vec3(0.0);
		int n = int(p.ghost_count + 0.5);
		for (int i = 1; i <= n; i++) {
			vec2 g = uv + ghost_vec * float(i);
			result += disperse(g, dir, p.chroma) * center_weight(g, asp);
		}

		vec2 toC = vec2(0.5) - uv;
		vec2 dir_uv = normalize(toC + vec2(1e-6));
		float radial = length(toC * asp);
		float aperture = 0.9 + 0.1 * cos(6.0 * atan(toC.y, toC.x));
		vec3 halo = vec3(0.0);
		for (int rr = 0; rr < 3; rr++) {
			float rw = p.halo_width * (0.6 + 0.35 * float(rr));
			float ring = clamp(1.0 - abs(radial - rw) / (rw * 0.5 + 1e-3), 0.0, 1.0);
			ring = pow(ring, 2.0);
			halo += disperse_b(uv + dir_uv * rw, dir_uv, p.chroma) * ring;
		}
		result += halo * aperture * p.halo_strength;
		imageStore(dst_img, coord, vec4(result, 1.0));
		return;
	}

	if (m == 3) {
		const int K = 24;
		vec3 acc = vec3(0.0);
		float wsum = 0.0;
		for (int i = -K; i <= K; i++) {
			float fx = float(i) / float(K);
			float w = exp(-fx * fx * 3.5);
			acc += texture(src_a, uv + vec2(fx * p.streak_length, 0.0)).rgb * w;
			wsum += w;
		}
		vec3 streak = acc / max(wsum, 1e-4) * p.streak_tint.rgb * p.streak_strength;

		if (p.cross_strength > 0.0) {
			float cross_len = p.streak_length * aspect * p.cross_length;
			vec3 vacc = vec3(0.0);
			float vwsum = 0.0;
			for (int i = -K; i <= K; i++) {
				float fy = float(i) / float(K);
				float w = exp(-fy * fy * 3.5);
				vacc += texture(src_a, uv + vec2(0.0, fy * cross_len)).rgb * w;
				vwsum += w;
			}
			streak += vacc / max(vwsum, 1e-4) * p.streak_tint.rgb * (p.streak_strength * p.cross_strength);
		}

		vec3 star = vec3(0.0);
		int sp = int(p.streak_tint.w + 0.5);
		if (sp > 0 && p.starburst_strength > 0.0) {
			const int SK = 16;
			float slen = p.streak_length * 0.7;
			float sden = 0.0;
			for (int a = 0; a < sp; a++) {
				float sa = PI * float(a) / float(sp);
				vec2 d = vec2(cos(sa), sin(sa) * aspect);
				for (int i = -SK; i <= SK; i++) {
					float fx = float(i) / float(SK);
					float w = exp(-fx * fx * 4.0);
					star += texture(src_a, uv + d * fx * slen).rgb * w;
					sden += w;
				}
			}
			star = star / max(sden, 1e-4) * p.streak_tint.rgb * p.starburst_strength;
		}
		imageStore(dst_img, coord, vec4(streak + star, 1.0));
		return;
	}

	vec3 feat = texture(src_a, uv).rgb;
	vec3 streak = texture(src_b, uv).rgb;
	vec3 flare = (feat + streak) * p.tint.rgb * p.intensity;
	if (p.has_dirt > 0.5) {
		float g = 0.0;
		for (int gy = 0; gy < 4; gy++) {
			for (int gx = 0; gx < 4; gx++) {
				g += luma(texture(src_d, (vec2(float(gx), float(gy)) + 0.5) / 4.0).rgb);
			}
		}
		g /= 16.0;
		flare += texture(src_c, uv).rgb * g * p.dirt_strength;
	}
	flare = flare * p.max_flare / (flare + vec3(p.max_flare));
	vec3 base = imageLoad(dst_img, coord).rgb;
	imageStore(dst_img, coord, vec4(base + max(flare, vec3(0.0)), 1.0));
}
