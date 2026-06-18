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

render_mode cull_disabled, depth_draw_opaque, alpha_to_coverage, specular_schlick_ggx;

group_uniforms Color;
uniform sampler2D tex_color : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 color_tint : source_color = vec4(1.0);
group_uniforms;

group_uniforms Surface;
uniform sampler2D tex_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool use_packed_nr = true;
uniform bool flip_normal_y = true;
uniform sampler2D tex_roughness : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float roughness_mul : hint_range(0.0, 2.0) = 1.0;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float specular : hint_range(0.0, 1.0) = 0.5;
group_uniforms;

group_uniforms Alpha;

uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.5;
group_uniforms;

group_uniforms Translucency;
uniform bool use_translucency = false;
uniform vec4 translucency_tint : source_color = vec4(0.30, 0.45, 0.15, 1.0);
uniform float translucency_strength : hint_range(0.0, 1.0) = 0.5;
group_uniforms;

group_uniforms UV;
uniform vec2 uv_scale = vec2(1.0, 1.0);
uniform vec2 uv_offset = vec2(0.0, 0.0);
group_uniforms;

void fragment() {
    vec2 uv = UV * uv_scale + uv_offset;

    vec4 c = texture(tex_color, uv);

    vec4 nr = texture(tex_normal, uv);
    vec3 normal_tex = nr.rgb;

    if (flip_normal_y) normal_tex.g = 1.0 - normal_tex.g;
    float rough = (use_packed_nr ? nr.a : texture(tex_roughness, uv).r) * roughness_mul;

    if (!FRONT_FACING) NORMAL = -NORMAL;

    ALBEDO = c.rgb * color_tint.rgb;
    NORMAL_MAP = normal_tex;
    ROUGHNESS = clamp(rough, 0.04, 1.0);
    METALLIC = metallic;
    SPECULAR = specular;

    ALPHA = clamp((c.a - alpha_cutoff) / max(fwidth(c.a), 0.0001) + 0.5, 0.0, 1.0);
    ALPHA_SCISSOR_THRESHOLD = 0.5;

    if (use_translucency) {
        BACKLIGHT = translucency_tint.rgb * translucency_strength;
    }
}
