#include <metal_stdlib>

using namespace metal;

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

struct TexCoords {
    float data[4];
};

struct ConstantBuffers {
    constant TexCoords* texCoord;
};

struct Textures
{
    texture2d<float, access::sample> texture;
    ulong padding_1;
    ulong padding_2;
    ulong padding_3;
    ulong padding_4;
    ulong padding_5;
    ulong padding_6;
    ulong padding_7;
    ulong padding_8;
    ulong padding_9;
    ulong padding_10;
    ulong padding_11;
    ulong padding_12;
    ulong padding_13;
    ulong padding_14;
    ulong padding_15;
    ulong padding_16;
    ulong padding_17;
    ulong padding_18;
    ulong padding_19;
    ulong padding_20;
    ulong padding_21;
    ulong padding_22;
    ulong padding_23;
    ulong padding_24;
    ulong padding_25;
    ulong padding_26;
    ulong padding_27;
    ulong padding_28;
    ulong padding_29;
    ulong padding_30;
    ulong padding_31;
    ulong padding_32;
    ulong padding_33;
    ulong padding_34;
    ulong padding_35;
    ulong padding_36;
    ulong padding_37;
    ulong padding_38;
    ulong padding_39;
    ulong padding_40;
    ulong padding_41;
    ulong padding_42;
    ulong padding_43;
    ulong padding_44;
    ulong padding_45;
    ulong padding_46;
    ulong padding_47;
    ulong padding_48;
    ulong padding_49;
    ulong padding_50;
    ulong padding_51;
    ulong padding_52;
    ulong padding_53;
    ulong padding_54;
    ulong padding_55;
    ulong padding_56;
    ulong padding_57;
    ulong padding_58;
    ulong padding_59;
    ulong padding_60;
    ulong padding_61;
    ulong padding_62;
    ulong padding_63;
    sampler sampler;
    ulong padding_65;
    ulong padding_66;
    ulong padding_67;
    ulong padding_68;
    ulong padding_69;
    ulong padding_70;
    ulong padding_71;
    ulong padding_72;
    ulong padding_73;
    ulong padding_74;
    ulong padding_75;
    ulong padding_76;
    ulong padding_77;
    ulong padding_78;
    ulong padding_79;
    ulong padding_80;
    ulong padding_81;
    ulong padding_82;
    ulong padding_83;
    ulong padding_84;
    ulong padding_85;
    ulong padding_86;
    ulong padding_87;
    ulong padding_88;
    ulong padding_89;
    ulong padding_90;
    ulong padding_91;
    ulong padding_92;
    ulong padding_93;
    ulong padding_94;
    ulong padding_95;
    ulong padding_96;
    ulong padding_97;
    ulong padding_98;
    ulong padding_99;
    ulong padding_100;
    ulong padding_101;
    ulong padding_102;
    ulong padding_103;
    ulong padding_104;
    ulong padding_105;
    ulong padding_106;
    ulong padding_107;
    ulong padding_108;
    ulong padding_109;
    ulong padding_110;
    ulong padding_111;
    ulong padding_112;
    ulong padding_113;
    ulong padding_114;
    ulong padding_115;
    ulong padding_116;
    ulong padding_117;
    ulong padding_118;
    ulong padding_119;
    ulong padding_120;
    ulong padding_121;
    ulong padding_122;
    ulong padding_123;
    ulong padding_124;
    ulong padding_125;
    ulong padding_126;
    ulong padding_127;
};

vertex CopyVertexOut vertexMain(uint vid [[vertex_id]],
                                constant ConstantBuffers &constant_buffers [[buffer(20)]]) {
    CopyVertexOut out;

    int low = vid & 1;
    int high = vid >> 1;
    out.uv.x = constant_buffers.texCoord->data[low];
    out.uv.y = constant_buffers.texCoord->data[2 + high];
    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

fragment float4 fragmentMain(CopyVertexOut in [[stage_in]],
                             constant Textures &textures [[buffer(22)]]) {
    return textures.texture.sample(textures.sampler, in.uv);
}
