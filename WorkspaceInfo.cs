using System;

namespace Inedo.BuildMasterExtensions.Plastic
{
    internal sealed class WorkspaceInfo
    {
        public WorkspaceInfo(string text)
        {
            var info = text.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            this.Name = info[0].Trim();
            this.Location = info[1].Trim();
        }

        public string Name { get; private set; }
        public string Location { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", this.Name, this.Location);
        }
    }
}
