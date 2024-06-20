#include <metal_stdlib>

using namespace metal;

kernel void kernelMain(constant int4& stride_arguments [[buffer(0)]],
                       device uint8_t* in_data [[buffer(1)]],
                       device uint8_t* out_data [[buffer(2)]],
                       uint3 thread_position_in_grid [[thread_position_in_grid]],
                       uint3 threads_per_threadgroup [[threads_per_threadgroup]],
                       uint3 threadgroups_per_grid [[threads_per_grid]])
{
    // Determine what slice of the stride copies this invocation will perform.

    int sourceStride = stride_arguments.x;
    int targetStride = stride_arguments.y;
    int bufferSize = stride_arguments.z;
    int sourceOffset = stride_arguments.w;

    int strideRemainder = targetStride - sourceStride;
    int invocations = int(threads_per_threadgroup.x * threadgroups_per_grid.x);

    int copiesRequired = bufferSize / sourceStride;

    // Find the copies that this invocation should perform.

    // - Copies that all invocations perform.
    int allInvocationCopies = copiesRequired / invocations;

    // - Extra remainder copy that this invocation performs.
    int index = int(thread_position_in_grid.x);
    int extra = (index < (copiesRequired % invocations)) ? 1 : 0;

    int copyCount = allInvocationCopies + extra;

    // Finally, get the starting offset. Make sure to count extra copies.

    int startCopy = allInvocationCopies * index + min(copiesRequired % invocations, index);

    int srcOffset = sourceOffset + startCopy * sourceStride;
    int dstOffset = startCopy * targetStride;

    // Perform the copies for this region
    for (int i = 0; i < copyCount; i++) {
        for (int j = 0; j < sourceStride; j++) {
            out_data[dstOffset++] = in_data[srcOffset++];
        }

        for (int j = 0; j < strideRemainder; j++) {
            out_data[dstOffset++] = uint8_t(0);
        }
    }
}
