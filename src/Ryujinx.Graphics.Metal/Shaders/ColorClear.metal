#include <metal_stdlib>

using namespace metal;

struct VertexOut {
    float4 position [[position]];
};

vertex VertexOut vertexMain(ushort vid [[vertex_id]]) {
    int low = vid & 1;
    int high = vid >> 1;

    VertexOut out;

    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

struct FragmentOut {
    float4 color [[color(COLOR_ATTACHMENT_INDEX)]];
};

fragment FragmentOut fragmentMain(VertexOut in [[stage_in]],
                                  constant float4& clear_color [[buffer(0)]]) {
    return {clear_color};
}
