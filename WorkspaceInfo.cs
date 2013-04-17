namespace Inedo.BuildMasterExtensions.Plastic
{
    internal sealed class WorkspaceInfo
    {
        public WorkspaceInfo(string path)
        {
            this.Location = path;
        }

        public string Location { get; private set; }

        public override string ToString()
        {
            return this.Location;
        }
    }
}
