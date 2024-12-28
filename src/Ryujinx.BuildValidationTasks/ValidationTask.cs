using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.BuildValidationTasks
{
    public interface ValidationTask
    {
        public bool Execute(string projectPath, bool isGitRunner);
    }
}
