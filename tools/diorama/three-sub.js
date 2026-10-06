// Só o que o diorama usa: o build (tools/diorama/build.mjs) descarta o resto do Three.js.
import {
  WebGLRenderer, WebGLRenderTarget, Scene, OrthographicCamera, Color, DirectionalLight, LightProbe, Fog,
  Mesh, InstancedMesh, Group, Object3D, BufferGeometry, BufferAttribute, Float32BufferAttribute,
  PlaneGeometry, CylinderGeometry, ConeGeometry, SphereGeometry, IcosahedronGeometry,
  TorusGeometry, BoxGeometry, CircleGeometry, Shape, ShapeGeometry,
  MeshStandardMaterial, MeshLambertMaterial, MeshBasicMaterial, ShaderMaterial,
  CanvasTexture, Vector3, Matrix4, Quaternion, Euler, MathUtils,
  SRGBColorSpace, NeutralToneMapping, PCFSoftShadowMap, DoubleSide, BackSide, RepeatWrapping,
  HalfFloatType, ObjectSpaceNormalMap, AdditiveBlending, LinearMipmapLinearFilter, LinearFilter,
} from "../art3d/node_modules/three/build/three.module.js";

globalThis.THREE = {
  WebGLRenderer, WebGLRenderTarget, Scene, OrthographicCamera, Color, DirectionalLight, LightProbe, Fog,
  Mesh, InstancedMesh, Group, Object3D, BufferGeometry, BufferAttribute, Float32BufferAttribute,
  PlaneGeometry, CylinderGeometry, ConeGeometry, SphereGeometry, IcosahedronGeometry,
  TorusGeometry, BoxGeometry, CircleGeometry, Shape, ShapeGeometry,
  MeshStandardMaterial, MeshLambertMaterial, MeshBasicMaterial, ShaderMaterial,
  CanvasTexture, Vector3, Matrix4, Quaternion, Euler, MathUtils,
  SRGBColorSpace, NeutralToneMapping, PCFSoftShadowMap, DoubleSide, BackSide, RepeatWrapping,
  HalfFloatType, ObjectSpaceNormalMap, AdditiveBlending, LinearMipmapLinearFilter, LinearFilter,
};
