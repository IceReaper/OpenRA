#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

in vec3 aPosition;
in vec2 aUv;
in vec4 aColor;

out vec2 vUv;
out vec4 vColor;

void main()
{
	gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
	vUv = aUv;
	vColor = aColor;
}
