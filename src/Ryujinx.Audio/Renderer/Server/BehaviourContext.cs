using System;
using System.Buffers;
using System.Diagnostics;
using static Ryujinx.Audio.Renderer.Common.BehaviourParameter;

namespace Ryujinx.Audio.Renderer.Server
{
    /// <summary>
    /// Behaviour context.
    /// </summary>
    /// <remarks>This handles features based on the audio renderer revision provided by the user.</remarks>
    public class BehaviourContext
    {
        /// <summary>
        /// The base magic of the Audio Renderer revision.
        /// </summary>
        public const int BaseRevisionMagic = ('R' << 0) | ('E' << 8) | ('V' << 16) | ('0' << 24);

        /// <summary>
        /// REV1: first revision.
        /// </summary>
        public const int Revision1 = 1 << 24;

        /// <summary>
        /// REV2: Added support for splitter and fix GC-ADPCM context not being provided to the DSP.
        /// </summary>
        /// <remarks>This was added in system update 2.0.0</remarks>
        public const int Revision2 = 2 << 24;

        /// <summary>
        /// REV3: Incremented the max pre-delay from 150 to 350 for the reverb command and removed the (unused) codec system.
        /// </summary>
        /// <remarks>This was added in system update 3.0.0</remarks>
        public const int Revision3 = 3 << 24;

        /// <summary>
        /// REV4: Added USB audio device support and incremented the rendering limit percent to 75%.
        /// </summary>
        /// <remarks>This was added in system update 4.0.0</remarks>
        public const int Revision4 = 4 << 24;

        /// <summary>
        /// REV5: <see cref="Parameter.VoiceInParameter.DecodingBehaviour"/>, <see cref="Parameter.VoiceInParameter.FlushWaveBufferCount"/> were added to voice.
        /// A new performance frame format (version 2) was added with support for more information about DSP timing.
        /// <see cref="Parameter.RendererInfoOutStatus"/> was added to supply the count of update done sent to the DSP.
        /// A new version of the command estimator was added to address timing changes caused by the voice changes.
        /// Additionally, the rendering limit percent was incremented to 80%.
        /// </summary>
        /// <remarks>This was added in system update 6.0.0</remarks>
        public const int Revision5 = 5 << 24;

        /// <summary>
        /// REV6: This fixed a bug in the biquad filter command not clearing up <see cref="Dsp.State.BiquadFilterState"/> with <see cref="Effect.UsageState.New"/> usage state.
        /// </summary>
        /// <remarks>This was added in system update 6.1.0</remarks>
        public const int Revision6 = 6 << 24;

        /// <summary>
        /// REV7: Client side (finally) doesn't send all the mix client state to the server and can do partial updates.
        /// </summary>
        /// <remarks>This was added in system update 8.0.0</remarks>
        public const int Revision7 = 7 << 24;

        /// <summary>
        /// REV8:
        /// Wavebuffer was changed to support more control over loop (you can now specify where to start and end a loop, and how many times to loop).
        /// <see cref="Parameter.VoiceInParameter.SrcQuality"/> was added (see <see cref="Parameter.VoiceInParameter.SampleRateConversionQuality"/> for more info).
        /// Final leftovers of the codec system were removed.
        /// <see cref="Common.SampleFormat.PcmFloat"/> support was added.
        /// A new version of the command estimator was added to address timing changes caused by the voice and command changes.
        /// </summary>
        /// <remarks>This was added in system update 9.0.0</remarks>
        public const int Revision8 = 8 << 24;

        /// <summary>
        /// REV9:
        /// EffectInfo parameters were revisited with a new revision (version 2) allowing more data control between the client and server.
        /// A new effect was added: Limiter. This effect is effectively implemented with a DRC while providing statistics on the processing on <see cref="Parameter.EffectOutStatusVersion2"/>.
        /// </summary>
        /// <remarks>This was added in system update 12.0.0</remarks>
        public const int Revision9 = 9 << 24;

        /// <summary>
        /// REV10:
        /// Added Bluetooth audio device support and removed the unused "GetAudioSystemMasterVolumeSetting" audio device API.
        /// A new effect was added: Capture. This effect allows the client side to capture audio buffers of a mix.
        /// A new command was added for double biquad filters on voices. This is implemented using a direct form 1 (instead of the usual direct form 2).
        /// A new version of the command estimator was added to support the new commands.
        /// </summary>
        /// <remarks>This was added in system update 13.0.0</remarks>
        public const int Revision10 = 10 << 24;

        /// <summary>
        /// REV11:
        /// The "legacy" effects (Delay, Reverb and Reverb 3D) were updated to match the standard channel mapping used by the audio renderer.
        /// A new effect was added: Compressor. This effect is effectively implemented with a DRC.
        /// A new version of the command estimator was added to address timing changes caused by the legacy effects changes.
        /// A voice drop parameter was added in 15.0.0: This allows an application to amplify or attenuate the estimated time of DSP commands.
        /// </summary>
        /// <remarks>This was added in system update 14.0.0 but some changes were made in 15.0.0</remarks>
        public const int Revision11 = 11 << 24;

        /// <summary>
        /// REV12:
        /// Two new commands were added to for biquad filtering and mixing (with optinal volume ramp) on the same command.
        /// Splitter destinations can now specify up to two biquad filtering parameters, used for filtering the buffer before mixing.
        /// </summary>
        /// <remarks>This was added in system update 17.0.0</remarks>
        public const int Revision12 = 12 << 24;

        /// <summary>
        /// REV13:
        /// The compressor effect can now output statistics.
        /// Splitter destinations now explicitly reset the previous mix volume, instead of doing so on first use.
        /// </summary>
        /// <remarks>This was added in system update 18.0.0</remarks>
        public const int Revision13 = 13 << 24;

        /// <summary>
        /// Last revision supported by the implementation.
        /// </summary>
        public const int LastRevision = Revision13;

        /// <summary>
        /// Target revision magic supported by the implementation.
        /// </summary>
        public const int ProcessRevision = BaseRevisionMagic + LastRevision;

        /// <summary>
        /// Get the revision number from the revision magic.
        /// </summary>
        /// <param name="revision">The revision magic.</param>
        /// <returns>The revision number.</returns>
        public static int GetRevisionNumber(int revision) => (revision - BaseRevisionMagic) >> 24;

        /// <summary>
        /// Current active revision.
        /// </summary>
        public int UserRevision { get; private set; }

        /// <summary>
        /// Error storage.
        /// </summary>
        private readonly ErrorInfo[] _errorInfos;

        /// <summary>
        /// Current position in the <see cref="_errorInfos"/> array.
        /// </summary>
        private uint _errorIndex;

        /// <summary>
        /// Current flags of the <see cref="BehaviourContext"/>.
        /// </summary>
        private ulong _flags;

        /// <summary>
        /// Create a new instance of <see cref="BehaviourContext"/>.
        /// </summary>
        public BehaviourContext()
        {
            UserRevision = 0;
            _errorInfos = new ErrorInfo[Constants.MaxErrorInfos];
            _errorIndex = 0;
        }

        /// <summary>
        /// Set the active revision.
        /// </summary>
        /// <param name="userRevision">The active revision.</param>
        public void SetUserRevision(int userRevision)
        {
            UserRevision = userRevision;
        }

        /// <summary>
        /// Update flags of the <see cref="BehaviourContext"/>.
        /// </summary>
        /// <param name="flags">The new flags.</param>
        public void UpdateFlags(ulong flags)
        {
            _flags = flags;
        }

        /// <summary>
        /// Check if a given revision is valid/supported.
        /// </summary>
        /// <param name="revision">The revision magic to check.</param>
        /// <returns>Returns true if the given revision is valid/supported</returns>
        public static bool CheckValidRevision(int revision)
        {
            return GetRevisionNumber(revision) <= GetRevisionNumber(ProcessRevision);
        }

        /// <summary>
        /// Check if the given revision is greater than or equal the supported revision.
        /// </summary>
        /// <param name="revision">The revision magic to check.</param>
        /// <param name="supportedRevision">The revision magic of the supported revision.</param>
        /// <returns>Returns true if the given revision is greater than or equal the supported revision.</returns>
        public static bool CheckFeatureSupported(int revision, int supportedRevision)
        {
            int revA = GetRevisionNumber(revision);
            int revB = GetRevisionNumber(supportedRevision);

            if (revA > LastRevision)
            {
                revA = 1;
            }

            if (revB > LastRevision)
            {
                revB = 1;
            }

            return revA >= revB;
        }

        /// <summary>
        /// Check if the memory pool mapping bypass flag is active.
        /// </summary>
        /// <returns>True if the memory pool mapping bypass flag is active.</returns>
        public bool IsMemoryPoolForceMappingEnabled()
        {
            return (_flags & 1) != 0;
        }

        /// <summary>
        /// Check if the audio renderer should fix the GC-ADPCM context not being provided to the DSP.
        /// </summary>
        /// <returns>True if the audio renderer should fix it.</returns>
        public bool IsAdpcmLoopContextBugFixed()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision2);
        }

        /// <summary>
        /// Check if the audio renderer should accept splitters.
        /// </summary>
        /// <returns>True if the audio renderer should accept splitters.</returns>
        public bool IsSplitterSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision2);
        }

        /// <summary>
        /// Check if the audio renderer should use a max pre-delay of 350 instead of 150.
        /// </summary>
        /// <returns>True if the max pre-delay must be 350.</returns>
        public bool IsLongSizePreDelaySupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision3);
        }

        /// <summary>
        /// Check if the audio renderer should expose USB audio device.
        /// </summary>
        /// <returns>True if the audio renderer should expose USB audio device.</returns>
        public bool IsAudioUsbDeviceOutputSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision4);
        }

        /// <summary>
        /// Get the percentage allocated to the audio renderer on the DSP for processing.
        /// </summary>
        /// <returns>The percentage allocated to the audio renderer on the DSP for processing.</returns>
        public float GetAudioRendererProcessingTimeLimit()
        {
            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5))
            {
                return 0.80f;
            }

            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision4))
            {
                return 0.75f;
            }

            return 0.70f;
        }

        /// <summary>
        /// Check if the audio render should support voice flushing.
        /// </summary>
        /// <returns>True if the audio render should support voice flushing.</returns>
        public bool IsFlushVoiceWaveBuffersSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5);
        }

        /// <summary>
        /// Check if the audio renderer should trust the user destination count in <see cref="Renderer.Server.Splitter.SplitterState.Update(Renderer.Server.Splitter.SplitterContext, Renderer.Parameter.SplitterInParameter, SequenceReader{byte})"/>.
        /// </summary>
        /// <returns>True if the audio renderer should trust the user destination count.</returns>
        public bool IsSplitterBugFixed()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5);
        }

        /// <summary>
        /// Check if the audio renderer should supply the elapsed frame count to the user when updating.
        /// </summary>
        /// <returns>True if the audio renderer should supply the elapsed frame count to the user when updating.</returns>
        public bool IsElapsedFrameCountSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5);
        }

        /// <summary>
        /// Get the performance metric data format version.
        /// </summary>
        /// <returns>The performance metric data format version.</returns>
        public uint GetPerformanceMetricsDataFormat()
        {
            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5))
            {
                return 2;
            }

            return 1;
        }

        /// <summary>
        /// Check if the audio renderer should support <see cref="Parameter.VoiceInParameter.DecodingBehaviour"/>.
        /// </summary>
        /// <returns>True if the audio renderer should support <see cref="Parameter.VoiceInParameter.DecodingBehaviour"/>.</returns>
        public bool IsDecodingBehaviourFlagSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5);
        }

        /// <summary>
        /// Check if the audio renderer should fix the biquad filter command not clearing up <see cref="Dsp.State.BiquadFilterState"/> with <see cref="Effect.UsageState.New"/> usage state.
        /// </summary>
        /// <returns>True if the biquad filter state should be cleared.</returns>
        public bool IsBiquadFilterEffectStateClearBugFixed()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision6);
        }

        /// <summary>
        /// Check if the audio renderer should accept partial mix updates.
        /// </summary>
        /// <returns>True if the audio renderer should accept partial mix updates.</returns>
        public bool IsMixInParameterDirtyOnlyUpdateSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision7);
        }

        /// <summary>
        /// Check if the audio renderer should use the new wavebuffer format.
        /// </summary>
        /// <returns>True if the audio renderer should use the new wavebuffer format.</returns>
        public bool IsWaveBufferVersion2Supported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision8);
        }

        /// <summary>
        /// Check if the audio renderer should use the new effect info format.
        /// </summary>
        /// <returns>True if the audio renderer should use the new effect info format.</returns>
        public bool IsEffectInfoVersion2Supported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision9);
        }

        /// <summary>
        /// Check if the audio renderer should use an optimized Biquad Filter (Direct Form 1) in case of two biquad filters are defined on a voice.
        /// </summary>
        /// <returns>True if the audio renderer should use the optimization.</returns>
        public bool UseMultiTapBiquadFilterProcessing()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision10);
        }

        /// <summary>
        /// Check if the audio renderer should support new channel resource mapping for 5.1 on Delay, Reverb and Reverb 3D effects.
        /// </summary>
        /// <returns>True if the audio renderer support new channel resource mapping for 5.1.</returns>
        public bool IsNewEffectChannelMappingSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision11);
        }

        /// <summary>
        /// Check if the audio renderer should support biquad filter on splitter.
        /// </summary>
        /// <returns>True if the audio renderer support biquad filter on splitter</returns>
        public bool IsBiquadFilterParameterForSplitterEnabled()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision12);
        }

        /// <summary>
        /// Check if the audio renderer should support explicit previous mix volume reset on splitter.
        /// </summary>
        /// <returns>True if the audio renderer support explicit previous mix volume reset on splitter</returns>
        public bool IsSplitterPrevVolumeResetSupported()
        {
            return CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision13);
        }

        /// <summary>
        /// Get the version of the <see cref="ICommandProcessingTimeEstimator"/>.
        /// </summary>
        /// <returns>The version of the <see cref="ICommandProcessingTimeEstimator"/>.</returns>
        public int GetCommandProcessingTimeEstimatorVersion()
        {
            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision11))
            {
                return 5;
            }

            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision10))
            {
                return 4;
            }

            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision8))
            {
                return 3;
            }

            if (CheckFeatureSupported(UserRevision, BaseRevisionMagic + Revision5))
            {
                return 2;
            }

            return 1;
        }

        /// <summary>
        /// Append a new <see cref="ErrorInfo"/> to the error array.
        /// </summary>
        /// <param name="errorInfo">The new <see cref="ErrorInfo"/> to add.</param>
        public void AppendError(ref ErrorInfo errorInfo)
        {
            Debug.Assert(errorInfo.ErrorCode == ResultCode.Success);

            if (_errorIndex <= Constants.MaxErrorInfos - 1)
            {
                _errorInfos[_errorIndex++] = errorInfo;
            }
        }

        /// <summary>
        /// Copy the internal <see cref="ErrorInfo"/> array to the given <see cref="Span{ErrorInfo}"/> and output the count copied.
        /// </summary>
        /// <param name="errorInfos">The output <see cref="Span{ErrorInfo}"/>.</param>
        /// <param name="errorCount">The output error count containing the count of <see cref="ErrorInfo"/> copied.</param>
        public void CopyErrorInfo(Span<ErrorInfo> errorInfos, out uint errorCount)
        {
            if (errorInfos.Length != Constants.MaxErrorInfos)
            {
                throw new ArgumentException("Invalid size of errorInfos span!");
            }

            errorCount = Math.Min(_errorIndex, Constants.MaxErrorInfos);

            for (int i = 0; i < Constants.MaxErrorInfos; i++)
            {
                if (i < errorCount)
                {
                    errorInfos[i] = _errorInfos[i];
                }
                else
                {
                    errorInfos[i] = new ErrorInfo
                    {
                        ErrorCode = 0,
                        ExtraErrorInfo = 0,
                    };
                }
            }
        }

        /// <summary>
        /// Clear the <see cref="ErrorInfo"/> array.
        /// </summary>
        public void ClearError()
        {
            _errorIndex = 0;
        }
    }
}
