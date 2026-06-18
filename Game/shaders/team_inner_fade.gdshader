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

render_mode unshaded, cull_back, depth_draw_never, shadows_disabled, blend_mix;

uniform vec4 team_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform float fresnel_power : hint_range(0.5, 16.0) = 6.0;
uniform float fresnel_max : hint_range(0.0, 1.0) = 0.55;
uniform float distance_alpha_near : hint_range(0.0, 50.0) = 4.0;
uniform float distance_alpha_far : hint_range(0.0, 100.0) = 25.0;
uniform float distance_min_alpha : hint_range(0.0, 1.0) = 0.0;
uniform float distance_max_alpha : hint_range(0.0, 1.0) = 1.0;

varying float v_dist_factor;

void vertex() {
	vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float view_dist = max(-view_pos.z, 0.001);
	float t = clamp((view_dist - distance_alpha_near) / max(distance_alpha_far - distance_alpha_near, 0.001), 0.0, 1.0);
	v_dist_factor = mix(distance_min_alpha, distance_max_alpha, t);
}

void fragment() {
	float ndotv = max(0.0, dot(normalize(NORMAL), normalize(VIEW)));
	float fresnel = pow(1.0 - ndotv, fresnel_power);
	ALBEDO = team_color.rgb;
	ALPHA = fresnel * fresnel_max * team_color.a * v_dist_factor;
}
