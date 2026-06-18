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

shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec3 dust_color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 dust_color_b : source_color = vec3(1.0, 0.84, 0.58);
uniform float brightness : hint_range(0.0, 4.0) = 1.2;
uniform float sparkle : hint_range(0.0, 1.0) = 0.12;
uniform float sparkle_ratio : hint_range(0.0, 1.0) = 0.3;
uniform float twinkle_speed : hint_range(0.0, 10.0) = 2.2;
uniform float soft_fade : hint_range(0.0, 4.0) = 0.5;
uniform float near_fade_start = 0.15;
uniform float near_fade_end = 0.7;
uniform float far_fade_start = 28.0;
uniform float far_fade_end = 55.0;
uniform float light_response : hint_range(0.0, 2.0) = 0.7;

uniform sampler2D depth_tex : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;

varying float v_seed;
varying float v_glitter;

float hash(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	mat4 bb = mat4(
		INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz),
		INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz),
		INV_VIEW_MATRIX[2] * length(MODEL_MATRIX[2].xyz),
		MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = VIEW_MATRIX * bb;
	v_seed = hash(float(INSTANCE_ID));
	v_glitter = hash(float(INSTANCE_ID) + 7.0);
}

void fragment() {
	vec2 uv = UV - vec2(0.5);
	float d = length(uv);

	float tw01 = sin(TIME * twinkle_speed + v_seed * 6.2831853) * 0.5 + 0.5;
	float base = smoothstep(0.5, 0.05, d);
	float core = pow(smoothstep(0.28, 0.0, d), 4.0);
	vec2 a = abs(uv);
	float star = smoothstep(0.5, 0.0, a.x) * smoothstep(0.05, 0.0, a.y)
		+ smoothstep(0.5, 0.0, a.y) * smoothstep(0.05, 0.0, a.x);
	float flash = smoothstep(0.55, 1.0, tw01);
	float glitter = step(v_glitter, sparkle_ratio);
	float shape = base * 0.25
		+ core * (0.25 + 0.75 * tw01)
		+ star * flash * sparkle * glitter;

	float frag_z = -VERTEX.z;

	float scene_raw = texture(depth_tex, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, scene_raw);
	vec4 vpos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	float scene_z = -vpos.z / vpos.w;
	float soft = clamp((scene_z - frag_z) / max(soft_fade, 1e-3), 0.0, 1.0);

	float near_k = smoothstep(near_fade_start, near_fade_end, frag_z);
	float far_k = 1.0 - smoothstep(far_fade_start, far_fade_end, frag_z);

	float bg_lum = dot(texture(screen_tex, SCREEN_UV).rgb, vec3(0.2126, 0.7152, 0.0722));
	float light = mix(1.0, 0.25 + 1.75 * smoothstep(0.05, 0.6, bg_lum), light_response);

	vec3 dust_color = mix(dust_color_a, dust_color_b, v_seed);
	ALBEDO = dust_color * brightness * shape * soft * near_k * far_k * light;
}
