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
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley, specular_schlick_ggx;

const int MAX_IMPACTS = 16;

group_uniforms glass;
uniform vec4 tint : source_color = vec4(0.62, 0.76, 0.85, 0.08);
uniform float metalness : hint_range(0.0, 1.0) = 0.0;
uniform float roughness : hint_range(0.0, 1.0) = 0.06;
uniform float specular : hint_range(0.0, 1.0) = 0.5;

group_uniforms frost;
uniform float frostedness : hint_range(0.0, 1.0) = 0.15;
uniform int samples : hint_range(1, 64) = 16;
uniform float blur_mip : hint_range(0.0, 6.0) = 2.0;

group_uniforms dirt;
uniform sampler2D dirt_texture : source_color, repeat_enable, filter_linear_mipmap, hint_default_black;
uniform float dirt_amount : hint_range(0.0, 2.0) = 1.0;
uniform vec3 dirt_color : source_color = vec3(0.28, 0.27, 0.24);
uniform vec2 dirt_tiling = vec2(1.0, 1.0);

group_uniforms edge;
uniform vec3 fresnel_color : source_color = vec3(1.0);
uniform float fresnel_amount : hint_range(0.0, 1.0) = 0.25;
uniform float fresnel_power : hint_range(1.0, 8.0) = 4.0;

group_uniforms cracks;
uniform int impact_count = 0;
uniform vec4 impacts[MAX_IMPACTS];
uniform vec2 pane_size = vec2(1.6, 2.2);
uniform vec3 crack_color : source_color = vec3(0.9, 0.95, 1.0);
uniform float crack_emission = 1.0;
uniform float line_width = 0.0035;
uniform float network_scale = 22.0;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;

float hash11(float p) {
	p = fract(p * 0.1031);
	p *= p + 33.33;
	p *= p + p;
	return fract(p);
}

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.103, 0.0973));
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

float radial_cracks(vec2 d, float r, float radius, float seed) {
	float spokes = floor(7.0 + hash11(seed) * 9.0);
	float a = atan(d.y, d.x) / 6.28318530718 + 0.5;
	float spoke = floor(a * spokes);
	a += (hash11(spoke + seed) - 0.5) * 0.7 / spokes;
	float s = fract(a * spokes);
	float ang_dist = min(s, 1.0 - s) / spokes * 6.28318530718 * r;
	float reach = (0.35 + 0.65 * hash11(spoke + seed * 1.7)) * radius;
	float len_fade = 1.0 - smoothstep(reach * 0.55, reach, r);
	float taper = line_width * (0.4 + 0.6 * (1.0 - r / max(radius, 0.001)));
	return (1.0 - smoothstep(0.0, taper, ang_dist)) * len_fade;
}

float network_cracks(vec2 p, float seed) {
	vec2 x = p * network_scale + seed;
	vec2 n = floor(x);
	vec2 f = fract(x);
	float f1 = 9.0;
	float f2 = 9.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = vec2(float(i), float(j));
			vec2 o = hash22(n + g);
			vec2 rr = g + o - f;
			float dd = dot(rr, rr);
			if (dd < f1) { f2 = f1; f1 = dd; }
			else if (dd < f2) { f2 = dd; }
		}
	}
	float edge = sqrt(f2) - sqrt(f1);
	return 1.0 - smoothstep(0.0, 0.06, edge);
}

float view_depth(float raw, vec2 uv, mat4 inv_proj) {
	vec3 ndc = vec3(uv * 2.0 - 1.0, raw);
	vec4 view = inv_proj * vec4(ndc, 1.0);
	return -(view.z / view.w);
}

void fragment() {
	vec2 p = UV * pane_size - pane_size * 0.5;

	float mask = 0.0;
	float hub = 0.0;
	for (int k = 0; k < impact_count; k++) {
		vec4 imp = impacts[k];
		vec2 d = p - imp.xy;
		float r = length(d);
		if (r > imp.z) continue;
		float fade = (1.0 - smoothstep(imp.z * 0.5, imp.z, r)) * imp.w;
		float seed = imp.x * 53.7 + imp.y * 131.3;
		float c = max(radial_cracks(d, r, imp.z, seed), network_cracks(p, seed) * (1.0 - smoothstep(0.0, imp.z * 0.7, r)) * 0.85);
		mask = max(mask, c * fade);
		hub = max(hub, (1.0 - smoothstep(0.0, line_width * 4.0, r)) * imp.w);
	}
	mask = clamp(max(mask, hub), 0.0, 1.0);

	float dirt = clamp(texture(dirt_texture, UV * dirt_tiling).r * dirt_amount, 0.0, 1.0);

	float fr = clamp(frostedness + mask * 0.6 + dirt * 0.25, 0.0, 1.0);
	float aspect = VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	float surf_depth = -VERTEX.z;
	float lod = roughness * blur_mip + fr * blur_mip;
	float radius = fr * 0.06;

	vec3 acc = textureLod(screen_tex, SCREEN_UV, lod).rgb;
	float total = 1.0;
	if (radius > 0.001) {
		float golden = 2.39996323;
		for (int i = 1; i <= samples; i++) {
			float fi = float(i);
			float t = fi / float(samples);
			float rr = radius * sqrt(t);
			float a = golden * fi;
			vec2 off = vec2(cos(a) / aspect, sin(a)) * rr;
			vec2 suv = SCREEN_UV + off;
			float sd = view_depth(texture(depth_tex, suv).r, suv, INV_PROJECTION_MATRIX);
			vec3 s = (sd < surf_depth - 0.05) ? acc : textureLod(screen_tex, suv, lod).rgb;
			float w = exp(-2.0 * t);
			acc += s * w;
			total += w;
		}
	}

	vec3 transmitted = mix(acc / total, tint.rgb, tint.a);
	transmitted = mix(transmitted, dirt_color, dirt);

	float fres = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), fresnel_power);

	ALBEDO = vec3(0.0);
	METALLIC = metalness;
	SPECULAR = specular;
	ROUGHNESS = clamp(roughness + mask * 0.5 + dirt * 0.3, 0.0, 1.0);
	EMISSION = transmitted + fresnel_color * fres * fresnel_amount + crack_color * mask * crack_emission;
	ALPHA = 1.0;
}
