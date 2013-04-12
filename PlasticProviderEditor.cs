using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Plastic
{
    /// <summary>
    /// Custom editor for the Plastic SCM provider.
    /// </summary>
    internal sealed class PlasticProviderEditor : ProviderEditorBase
    {
        private ValidatingTextBox txtWorkspace;
        private SourceControlFileFolderPicker txtCMExecutablePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlasticProviderEditor"/> class.
        /// </summary>
        public PlasticProviderEditor()
        {
        }

        protected override void CreateChildControls()
        {
            this.txtWorkspace = new ValidatingTextBox()
            {
                ID = "txtWorkspace",
                Required = true
            };

            this.txtCMExecutablePath = new SourceControlFileFolderPicker() { ServerId = this.EditorContext.ServerId };
            
            CUtil.Add(this,
                new FormFieldGroup("Plastic Executable Path",
                    "The path to the CM executable on the server.",
                    false,
                    new StandardFormField("CM Executable:", txtCMExecutablePath)),
                new FormFieldGroup("Workspace",
                    "Specifies the name of the Plastic SCM workspace this provider is associated with. This workspace must exist on any server which interacts with Plastic SCM.",
                    false,
                    new StandardFormField("Workspace Name:", txtWorkspace)
                )
            );
        }

        public override void BindToForm(ProviderBase extension)
        {
            EnsureChildControls();

            var ext = (PlasticProvider)extension;
            this.txtWorkspace.Text = ext.Workspace ?? string.Empty;
            this.txtCMExecutablePath.Text = ext.ExePath ?? string.Empty;
        }

        public override ProviderBase CreateFromForm()
        {
            EnsureChildControls();

            return new PlasticProvider()
            {
                Workspace = this.txtWorkspace.Text,
                ExePath = this.txtCMExecutablePath.Text
            };
        }
    }
}
