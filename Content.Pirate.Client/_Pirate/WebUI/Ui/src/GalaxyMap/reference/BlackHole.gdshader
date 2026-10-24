shader_type canvas_item;
render_mode blend_mix;

uniform float pixels : hint_range(10,100) = 100.0;

uniform vec4[3] colors : source_color;

uniform float radius : hint_range(0.0,0.5) = 0.5;
uniform float light_width : hint_range(0.0, 0.5) = 0.05;


void fragment() {
	// pixelize uv
	vec2 uv = floor(UV*pixels)/pixels;
	
	// distance from center
	float d_to_center = distance(uv, vec2(0.5));
	
	vec4 col = colors[0];	
	if (d_to_center > radius - light_width) {
		col = colors[1];
	}	
	if (d_to_center > radius - light_width * 0.5) {
		col = colors[2];
	}
	
	float a = step(d_to_center, radius);
	COLOR = vec4(col.rgb, a * col.a);
}
