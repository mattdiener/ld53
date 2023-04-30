shader_type spatial;
render_mode world_vertex_coords, unshaded;

vec4 fresnel_glow(float amount, float intensity, vec4 color, vec3 normal, vec3 view)
{
	return pow((1.0 - dot(normalize(normal), normalize(view))), amount) * color * intensity;
}

vec2 adjustedUV(vec2 uv, vec2 scale, vec2 offset) {
	return scale * uv + offset;
}

float yCoordAtAdjustedUV(vec2 uv, sampler2D heightmap, sampler2D heightmap2, float span, float valley, float time) {
	vec4 heightData = texture(heightmap, uv);
	vec4 heightData2 = texture(heightmap, uv);
	float val1 = sin(time + heightData.x*PI);
	float val2 = sin(time * 1.3 + heightData2.x*PI);
	return (val1 + val2) * span / 2.0 - valley;
}

vec3 normalAtAdjustedUV(vec2 uv, sampler2D heightmap, sampler2D heightmap2, float span, float time) {
	vec2 eh = vec2(0.002, 0.0);
	vec2 ev = vec2(0.0, 0.002);
	float t1v = sin(time + texture(heightmap, uv + ev).x*PI);
	float t2v = sin(time*1.3 + texture(heightmap2, uv + ev).x*PI);
	float t = (t1v + t2v) * 0.5 * span;
	
	float b1v = sin(time + texture(heightmap, uv - ev).x*PI);
	float b2v = sin(time*1.3 + texture(heightmap2, uv - ev).x*PI);
	float b = (t1v + t2v) * 0.5 * span;
	
	float r1v = sin(time + texture(heightmap, uv - eh).x*PI);
	float r2v = sin(time*1.3 + texture(heightmap2, uv - eh).x*PI);
	float r = (t1v + t2v) * 0.5 * span;
	
	float l1v = sin(time + texture(heightmap, uv + eh).x*PI);
	float l2v = sin(time*1.3 + texture(heightmap2, uv + eh).x*PI);
	float l = (t1v + t2v) * 0.5 * span;
	
	return normalize(vec3(r-l, 2.0, b-t));
}

float lerp(float a, float b, float t) {
	return b*t + a*(1.0-t);
}

/*
vec2 lerpv2(vec2 a, vec2 b, float t) {
	return b*t + a*(1.0-t);
}
*/

vec3 lerpv3(vec3 a, vec3 b, float t) {
	return b*t + a*(1.0-t);
}

vec4 lerpv4(vec4 a, vec4 b, float t) {
	return b*t + a*(1.0-t);
}

uniform sampler2D waveNoise;
uniform sampler2D waveNoise2;
uniform float unscaledVertexDistance;
uniform float span;
uniform float valley;
uniform vec2 scale;
void vertex() {
	vec2 offset = INSTANCE_CUSTOM.xy;
	int scaleBoundaries = int(INSTANCE_CUSTOM.z);
	
	// No harm in just baking in this const lookup table even though half of it is never touched.
	const vec2 boundaryMults[15] = {
		vec2(0,0),
		vec2(1,0),
		vec2(1,0),
		vec2(0,0),
		vec2(0,1),
		vec2(0,0),
		vec2(0,0),
		vec2(0,0),
		vec2(0,1),
		vec2(0,0),
		vec2(0,0),
		vec2(0,0),
		vec2(0,0),
		vec2(0,0),
		vec2(0,0)
	};
	
	// Powers of 2 ensures no overlaps on corners
	int boundaryIndex = 0;
	boundaryIndex += 1 * int(UV.y == 0.0);
	boundaryIndex += 2 * int(UV.y == 1.0);
	boundaryIndex += 4 * int(UV.x == 0.0);
	boundaryIndex += 8 * int(UV.x == 1.0);
	
	int isOdd = VERTEX_ID & 1;
	vec2 offsetMult = boundaryMults[(boundaryIndex & scaleBoundaries) * isOdd];
	
	vec2 adjUv = adjustedUV(UV, scale, offset);
	vec2 uvA = adjustedUV(UV + (offsetMult * unscaledVertexDistance), scale, offset);
	vec2 uvB = adjustedUV(UV - (offsetMult * unscaledVertexDistance), scale, offset);
	
	UV = adjUv;
	
	float ySampleA = yCoordAtAdjustedUV(uvA, waveNoise, waveNoise2, span, valley, TIME);
    float ySampleB = yCoordAtAdjustedUV(uvB, waveNoise, waveNoise2, span, valley, TIME);
	VERTEX.y += lerp(ySampleA, ySampleB, 0.5);
	
	vec3 normalSampleA = normalAtAdjustedUV(uvA, waveNoise, waveNoise2, span, TIME);
	vec3 normalSampleB = normalAtAdjustedUV(uvB, waveNoise, waveNoise2, span, TIME);
	NORMAL = lerpv3(normalSampleA, normalSampleB, 0.5);
}

void fragment() {	
	vec4 baseColor = vec4(0.4, 0.16, 0.4, 0.93);
	vec4 deepColor = vec4(0, 0.1, 0.2, 0.98);
	vec4 fresnel_color = vec4(0.6, 0.6, 0.6, 1.0);
	vec4 fresnel = fresnel_glow(8.0, 3, fresnel_color, NORMAL, VIEW);
	
	ALPHA = baseColor.a;//color.a;
	ALBEDO = baseColor.rgb + fresnel.rgb;
	ROUGHNESS = 0.7;
	SPECULAR = 0.2;
}
