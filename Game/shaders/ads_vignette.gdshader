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

shader_type canvas_item;
render_mode unshaded;

uniform float ads_blend : hint_range(0.0, 1.0) = 0.0;
uniform float vignette_inner : hint_range(0.0, 1.0) = 0.30;
uniform float vignette_outer : hint_range(0.0, 1.5) = 0.85;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.55;
uniform vec3 vignette_color : source_color = vec3(0.0, 0.0, 0.0);

void fragment() {
	vec2 uv = UV;

	vec2 centered = (uv - vec2(0.5)) * vec2(1.0, 0.85);
	float dist = length(centered) * 2.0;
	float v = smoothstep(vignette_inner, vignette_outer, dist);
	float alpha = v * vignette_strength * ads_blend;
	COLOR = vec4(vignette_color, alpha);
}
