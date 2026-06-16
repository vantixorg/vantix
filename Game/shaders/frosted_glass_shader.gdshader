shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

group_uniforms glass;
uniform vec3 tint : source_color = vec3(0.9, 0.95, 1.0);
uniform float tint_amount : hint_range(0.0, 1.0) = 0.1;
uniform float metalness : hint_range(0.0, 1.0) = 0.0;
uniform float roughness : hint_range(0.0, 1.0) = 0.08;
uniform float specular : hint_range(0.0, 1.0) = 0.5;

group_uniforms frost;
uniform float frostedness : hint_range(0.0, 1.0) = 0.5;
uniform int samples : hint_range(1, 64) = 16;
uniform float blur_mip : hint_range(0.0, 6.0) = 2.0;

group_uniforms refraction;
uniform bool use_normal_map = false;
uniform sampler2D normal_map : hint_normal;
uniform float normal_strength : hint_range(0.0, 4.0) = 1.0;
uniform float refraction : hint_range(0.0, 0.25) = 0.04;

group_uniforms edge;
uniform vec3 fresnel_color : source_color = vec3(1.0);
uniform float fresnel_amount : hint_range(0.0, 1.0) = 0.25;
uniform float fresnel_power : hint_range(1.0, 8.0) = 4.0;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;

float view_depth(float raw, vec2 uv, mat4 inv_proj) {
	vec3 ndc = vec3(uv * 2.0 - 1.0, raw);
	vec4 view = inv_proj * vec4(ndc, 1.0);
	return -(view.z / view.w);
}

void fragment() {
	vec2 distort = vec2(0.0);
	if (use_normal_map) {
		vec3 nrm = texture(normal_map, UV).xyz * 2.0 - 1.0;
		distort = nrm.xy * normal_strength * refraction;
	}

	float aspect = VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	vec2 base_uv = SCREEN_UV + distort;
	float surf_depth = -VERTEX.z;

	float radius = frostedness * 0.06;
	float lod = roughness * blur_mip + frostedness * blur_mip;
	float golden = 2.39996323;

	vec3 center = textureLod(screen_tex, base_uv, lod).rgb;
	vec3 acc = center;
	float total = 1.0;

	for (int i = 1; i <= samples; i++) {
		float fi = float(i);
		float t = fi / float(samples);
		float r = radius * sqrt(t);
		float a = golden * fi;
		vec2 off = vec2(cos(a) / aspect, sin(a)) * r;
		vec2 suv = base_uv + off;
		float sd = view_depth(texture(depth_tex, suv).r, suv, INV_PROJECTION_MATRIX);
		vec3 s = (sd < surf_depth - 0.05) ? center : textureLod(screen_tex, suv, lod).rgb;
		float w = exp(-2.0 * t);
		acc += s * w;
		total += w;
	}

	vec3 transmitted = mix(acc / total, tint, tint_amount);
	float fres = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), fresnel_power);

	ALBEDO = vec3(0.0);
	METALLIC = metalness;
	ROUGHNESS = roughness;
	SPECULAR = specular;
	if (use_normal_map) {
		NORMAL_MAP = texture(normal_map, UV).xyz;
		NORMAL_MAP_DEPTH = normal_strength;
	}
	EMISSION = transmitted + fresnel_color * fres * fresnel_amount;
	ALPHA = 1.0;
}
