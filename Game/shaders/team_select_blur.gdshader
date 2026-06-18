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

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float mip_level : hint_range(0.0, 8.0) = 5.0;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 0.35);

void fragment() {
	vec3 blurred = textureLod(screen_tex, SCREEN_UV, mip_level).rgb;
	COLOR = vec4(mix(blurred, tint.rgb, tint.a), 1.0);
}
