#include <metal_stdlib>

using namespace metal;

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

struct Textures
{
    texture2d_ms<float, access::read> texture;
};

fragment float4 fragmentMain(CopyVertexOut in [[stage_in]],
                             constant Textures &textures [[buffer(TEXTURES_INDEX)]],
                             uint sample_id [[sample_id]]) {
    uint2 tex_size = uint2(textures.texture.get_width(), textures.texture.get_height());
    uint2 tex_coord = uint2(in.uv * float2(tex_size));
    return textures.texture.read(tex_coord, sample_id);
}
