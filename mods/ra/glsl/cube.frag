#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

in vec2 vUv;
in vec4 vColor;

out vec4 fColor;

void main()
{
	fColor = vColor;
}
