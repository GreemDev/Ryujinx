using NUnit.Framework;
using Ryujinx.Audio.Renderer.Server;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class BehaviourContextTests
    {
        [Test]
        public void TestCheckFeature()
        {
            int latestRevision = BehaviourContext.BaseRevisionMagic + BehaviourContext.LastRevision;
            int previousRevision = BehaviourContext.BaseRevisionMagic + (BehaviourContext.LastRevision - 1);
            int invalidRevision = BehaviourContext.BaseRevisionMagic + (BehaviourContext.LastRevision + 1);

            Assert.IsTrue(BehaviourContext.CheckFeatureSupported(latestRevision, latestRevision));
            Assert.IsFalse(BehaviourContext.CheckFeatureSupported(previousRevision, latestRevision));
            Assert.IsTrue(BehaviourContext.CheckFeatureSupported(latestRevision, previousRevision));
            // In case we get an invalid revision, this is supposed to auto default to REV1 internally.. idk what the hell Nintendo was thinking here..
            Assert.IsTrue(BehaviourContext.CheckFeatureSupported(invalidRevision, latestRevision));
        }

        [Test]
        public void TestsMemoryPoolForceMappingEnabled()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision1);

            Assert.IsFalse(behaviourContext.IsMemoryPoolForceMappingEnabled());

            behaviourContext.UpdateFlags(0x1);

            Assert.IsTrue(behaviourContext.IsMemoryPoolForceMappingEnabled());
        }

        [Test]
        public void TestRevision1()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision1);

            Assert.IsFalse(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsFalse(behaviourContext.IsSplitterSupported());
            Assert.IsFalse(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsFalse(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsFalse(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsFalse(behaviourContext.IsSplitterBugFixed());
            Assert.IsFalse(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsFalse(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(1, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision2()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision2);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsFalse(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsFalse(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsFalse(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsFalse(behaviourContext.IsSplitterBugFixed());
            Assert.IsFalse(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsFalse(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(1, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision3()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision3);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsFalse(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsFalse(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsFalse(behaviourContext.IsSplitterBugFixed());
            Assert.IsFalse(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsFalse(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(1, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision4()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision4);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsFalse(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsFalse(behaviourContext.IsSplitterBugFixed());
            Assert.IsFalse(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsFalse(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.75f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(1, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision5()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision5);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision6()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision6);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsFalse(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision7()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision7);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsFalse(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision8()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision8);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsFalse(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(3, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision9()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision9);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsTrue(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsFalse(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(3, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision10()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision10);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsTrue(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsTrue(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsFalse(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(4, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision11()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision11);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsTrue(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsTrue(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsTrue(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsFalse(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(5, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision12()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision12);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsTrue(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsTrue(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsTrue(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsFalse(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(5, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision13()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision13);

            Assert.IsTrue(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.IsTrue(behaviourContext.IsSplitterSupported());
            Assert.IsTrue(behaviourContext.IsLongSizePreDelaySupported());
            Assert.IsTrue(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.IsTrue(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.IsTrue(behaviourContext.IsSplitterBugFixed());
            Assert.IsTrue(behaviourContext.IsElapsedFrameCountSupported());
            Assert.IsTrue(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.IsTrue(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.IsTrue(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.IsTrue(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.IsTrue(behaviourContext.UseMultiTapBiquadFilterProcessing());
            Assert.IsTrue(behaviourContext.IsNewEffectChannelMappingSupported());
            Assert.IsTrue(behaviourContext.IsBiquadFilterParameterForSplitterEnabled());
            Assert.IsTrue(behaviourContext.IsSplitterPrevVolumeResetSupported());

            Assert.AreEqual(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.AreEqual(5, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.AreEqual(2, behaviourContext.GetPerformanceMetricsDataFormat());
        }
    }
}
