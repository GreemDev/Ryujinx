namespace Ryujinx.Graphics.GAL.Multithreading.Commands
{
    struct DispatchComputeCommand : IGALCommand, IGALCommand<DispatchComputeCommand>
    {
        public readonly CommandType CommandType => CommandType.DispatchCompute;
        private int _groupsX;
        private int _groupsY;
        private int _groupsZ;
        private int _groupSizeX;
        private int _groupSizeY;
        private int _groupSizeZ;

        public void Set(int groupsX, int groupsY, int groupsZ, int groupSizeX, int groupSizeY, int groupSizeZ)
        {
            _groupsX = groupsX;
            _groupsY = groupsY;
            _groupsZ = groupsZ;
            _groupSizeX = groupSizeX;
            _groupSizeY = groupSizeY;
            _groupSizeZ = groupSizeZ;
        }

        public static void Run(ref DispatchComputeCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            renderer.Pipeline.DispatchCompute(command._groupsX, command._groupsY, command._groupsZ, command._groupSizeX, command._groupSizeY, command._groupSizeZ);
        }
    }
}
