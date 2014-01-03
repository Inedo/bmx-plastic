using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Agents;

namespace Inedo.BuildMasterExtensions.Plastic
{
    /// <summary>
    /// Custom editor for the Plastic SCM provider.
    /// </summary>
    internal sealed class PlasticProviderEditor : ProviderEditorBase
    {
        private ValidatingTextBox txtWorkspace;
        private SourceControlFileFolderPicker txtCMExecutablePath;

        private Button btnLoadRepositories;
        private Literal htmlBreak;
        private DropDownList ddlRepositories;
        private RequiredFieldValidator rfvRepository;
        private HiddenField hfCreatedTicks;

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
                Required = true,
                ReadOnly=true,
                Enabled = false,
                Text = "Auto-Generated"
            };

            this.txtCMExecutablePath = new SourceControlFileFolderPicker() { ServerId = this.EditorContext.ServerId, ValidationGroup="LoadRepos" };

            this.btnLoadRepositories = new Button()
            {
                ID = "hlbLoadRepositories",
                Text = "Load Repositories",
                ValidationGroup = "LoadRepos"
            };
            this.btnLoadRepositories.Click += btnLoadRepositories_Click;

            this.ddlRepositories = new DropDownList()
            {
                ID = "ddlRepositories",
                Enabled = false,
            };
            this.ddlRepositories.Items.Add(new ListItem("Enter CM Executible Path and Click Load", ""));

            this.rfvRepository = new RequiredFieldValidator()
            {
                ID="rfvRepository",
                ControlToValidate="ddlRepositories",
                Text="*"
            };

            this.htmlBreak = new Literal()
            {
                Text = "<br />"
            };

            this.hfCreatedTicks = new HiddenField()
            {
                Value = ""
            };
            
            CUtil.Add(this,
                new FormFieldGroup("Plastic Executable Path",
                    "The path to the CM executable on the server.",
                    false,
                    new StandardFormField("CM Executable:", txtCMExecutablePath)),
                new FormFieldGroup("Repository",
                    "Specifies the repository that this provider is associated with.",
                    false,
                    new StandardFormField("Repository Name:", btnLoadRepositories, htmlBreak, ddlRepositories, rfvRepository, hfCreatedTicks)
                )
            );
        }

        void btnLoadRepositories_Click(object sender, System.EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtCMExecutablePath.Text))
            {
                var curSelected = ddlRepositories.SelectedValue;
                ddlRepositories.Items.Clear();

                using (var agent = Util.Agents.CreateAgentFromId(this.EditorContext.ServerId))
                {
                    var svc = agent.GetService<IRemoteMethodExecuter>();
                    var repos = svc.InvokeFunc(PlasticProvider.GetRepositories, txtCMExecutablePath.Text);
                
                    if (repos != null && repos.Count > 0)
                    {
                        foreach (var r in repos) {
                            ddlRepositories.Items.Add(r);
                        }
                        ddlRepositories.Enabled = true;
                    }
                }                
                
                if (ddlRepositories.Items.FindByValue(curSelected) != null)
                    ddlRepositories.SelectedValue = curSelected;
            }
        }

        public override void BindToForm(ProviderBase extension)
        {
            EnsureChildControls();

            var ext = (PlasticProvider)extension;
            this.txtCMExecutablePath.Text = ext.ExePath ?? string.Empty;
            if (this.ddlRepositories.Items.FindByValue(ext.RepositoryName) == null)
                this.ddlRepositories.Items.Add(ext.RepositoryName);
            this.ddlRepositories.SelectedValue = ext.RepositoryName;
            this.hfCreatedTicks.Value = ext.CreatedTicks.ToString();
        }

        public override ProviderBase CreateFromForm()
        {
            EnsureChildControls();

            long createdTicks;

            if (string.IsNullOrEmpty(this.hfCreatedTicks.Value) || !long.TryParse(this.hfCreatedTicks.Value, out createdTicks))
            {
                createdTicks = System.DateTime.Now.Ticks;
                this.hfCreatedTicks.Value = createdTicks.ToString();
            }

            return new PlasticProvider()
            {
                RepositoryName = this.ddlRepositories.SelectedValue,
                CreatedTicks = createdTicks,
                ExePath = this.txtCMExecutablePath.Text
            };
        }
    }
}
