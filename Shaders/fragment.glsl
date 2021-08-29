#version 460 core

in vec3 VertexColor;

out vec4 final_fragment;

void main()
{
	final_fragment = vec4(VertexColor, 1.0f);
}