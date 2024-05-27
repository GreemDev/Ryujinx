#include <metal_stdlib>

using namespace metal;

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

vertex CopyVertexOut vertexMain(uint vid [[vertex_id]],
                                const device float* texCoord [[buffer(0)]]) {
    CopyVertexOut out;

    int low = vid & 1;
    int high = vid >> 1;
    out.uv.x = texCoord[low];
    out.uv.y = texCoord[2 + high];
    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

fragment float4 fragmentMain(CopyVertexOut in [[stage_in]],
                             texture2d<float, access::sample> texture [[texture(0)]],
                             sampler sampler [[sampler(0)]]) {
    return texture.sample(sampler, in.uv);
}
