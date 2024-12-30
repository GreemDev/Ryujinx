namespace Ryujinx.BuildValidationTasks
{
    public interface ValidationTask
    {
        public bool Execute(string projectPath, bool isGitRunner);
    }
}
