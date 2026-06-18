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

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float sprint : hint_range(0.0, 1.0) = 0.0;
uniform float strength : hint_range(0.0, 0.06) = 0.032;

void fragment() {
	vec2 uv = SCREEN_UV;

	float ex = abs(uv.x - 0.5) * 2.0;
	float ey = abs(uv.y - 0.5) * 2.0;
	float edge = clamp(max(ex * 1.1, ey * 0.55), 0.0, 1.0);
	edge = edge * edge;
	float amt = edge * sprint;

	vec3 c;
	if (amt < 0.001) {

		c = texture(screen_tex, uv).rgb;
	} else {

		vec2 dir = uv - vec2(0.5);
		c = vec3(0.0);
		for (int i = 0; i < 8; i++) {
			float t = float(i) / 7.0;
			c += texture(screen_tex, uv - dir * t * strength * amt).rgb;
		}
		c /= 8.0;
	}

	COLOR = vec4(c, 1.0);
}
