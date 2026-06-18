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

layout(rgba16f, set = 0, binding = 0) uniform image2D color_image;
layout(set = 0, binding = 1) uniform sampler2D depth_tex;

layout(push_constant, std430) uniform Params {
	mat4 inv_view_proj;
	vec3 camera_pos;
	float time;
	vec2 raster_size;
	float brightness;
	float density;
	float sparkle;
	float twinkle_speed;
	float near_fade;
	float far_fade;
	float cell_size;
	float mote_radius;
	float start_dist;
	float coverage;
} p;

const vec3 COLOR_A = vec3(1.0, 1.0, 1.0);
const vec3 COLOR_B = vec3(1.0, 0.84, 0.58);
const int LAYERS = 24;
const float HAZE = 0.5;
const vec3 DRIFT = vec3(0.10, -0.025, 0.06);

float hash13(vec3 q) {
	q = fract(q * 0.1031);
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = x - i;
	f = f * f * (3.0 - 2.0 * f);
	float n000 = hash13(i);
	float n100 = hash13(i + vec3(1.0, 0.0, 0.0));
	float n010 = hash13(i + vec3(0.0, 1.0, 0.0));
	float n110 = hash13(i + vec3(1.0, 1.0, 0.0));
	float n001 = hash13(i + vec3(0.0, 0.0, 1.0));
	float n101 = hash13(i + vec3(1.0, 0.0, 1.0));
	float n011 = hash13(i + vec3(0.0, 1.0, 1.0));
	float n111 = hash13(i + vec3(1.0, 1.0, 1.0));
	return mix(
		mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y),
		mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y), f.z);
}

float mote(vec3 wp, float rcell, out float seed) {
	vec3 g = wp / p.cell_size;
	vec3 cell = floor(g);
	seed = hash13(cell);
	if (seed < p.coverage) {
		return 0.0;
	}
	float r = min(rcell * mix(0.5, 1.3, hash13(cell + 12.3)), 0.45);
	vec3 c = r + (1.0 - 2.0 * r) * vec3(hash13(cell + 1.7), hash13(cell + 4.3), hash13(cell + 8.9));
	float d = length((g - cell) - c);
	float glow = smoothstep(r, 0.0, d);
	float tw = sin(p.time * p.twinkle_speed + seed * 6.2831853) * 0.5 + 0.5;
	float core = pow(glow, 8.0) * tw * p.sparkle;
	return glow * (0.35 + 0.65 * tw) + core;
}

void main() {
	ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
	if (coord.x >= int(p.raster_size.x) || coord.y >= int(p.raster_size.y)) {
		return;
	}

	vec2 uv = (vec2(coord) + 0.5) / p.raster_size;
	vec2 ndc = uv * 2.0 - 1.0;

	vec4 far_h = p.inv_view_proj * vec4(ndc, 0.0, 1.0);
	vec3 ray_dir = normalize(far_h.xyz / far_h.w - p.camera_pos);

	float depth = texture(depth_tex, uv).r;
	float scene_dist;
	if (depth <= 0.0) {
		scene_dist = 1.0e9;
	} else {
		vec4 sp = p.inv_view_proj * vec4(ndc, depth, 1.0);
		scene_dist = length(sp.xyz / sp.w - p.camera_pos);
	}

	float march_end = min(scene_dist, p.far_fade);
	vec4 src = imageLoad(color_image, coord);
	if (march_end <= p.start_dist) {
		return;
	}

	float ratio = march_end / p.start_dist;
	vec3 drift = DRIFT * p.time;
	vec3 sparkle = vec3(0.0);
	float haze = 0.0;
	for (int i = 0; i < LAYERS; i++) {
		float t = (float(i) + 0.5) / float(LAYERS);
		float dist = p.start_dist * pow(ratio, t);
		if (dist >= scene_dist) {
			break;
		}
		float near_k = smoothstep(0.0, p.near_fade, dist);
		float far_k = 1.0 - smoothstep(p.far_fade * 0.6, p.far_fade, dist);
		float dens = near_k * far_k;
		if (dens <= 0.0) {
			continue;
		}
		vec3 wp = p.camera_pos + ray_dir * dist - drift;
		float n = vnoise(wp * 0.09) * 0.65 + vnoise(wp * 0.22) * 0.35;
		float cl = 0.35 + 0.65 * smoothstep(0.35, 0.85, n);
		float rcell = p.mote_radius * dist / p.cell_size;
		float seed;
		float m = mote(wp, rcell, seed);
		sparkle += mix(COLOR_A, COLOR_B, seed) * m * dens * cl;
		haze += smoothstep(0.5, 0.92, n) * dens;
	}

	float lum = dot(src.rgb, vec3(0.2126, 0.7152, 0.0722));
	float light = smoothstep(0.02, 0.4, lum);
	vec3 col = src.rgb;
	col += sparkle * (0.3 + 0.9 * light) * (p.brightness * p.density * 0.25);
	col += COLOR_B * haze * (0.08 + 1.5 * light) * (p.brightness * p.density * HAZE / float(LAYERS));
	imageStore(color_image, coord, vec4(col, src.a));
}
