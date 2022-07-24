using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using APAS.MotionLib.ZMC.ConfigurationEditor.DataTemplateSelectors;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.UserControls
{
    /// <summary>
    /// Interaction logic for AxisSettingsEditor.xaml
    /// </summary>
    public partial class AxisSettingsEditor : UserControl
    {
        private readonly AxisSettingsCellTemplateSelector _cellTempSelector;

        public AxisSettingsEditor()
        {
            InitializeComponent();

            _cellTempSelector = Resources["cellTemplateSelector"] as AxisSettingsCellTemplateSelector;
        }

        private void DataGrid_OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            DisplayAttribute dispAttr = null;

            switch (e.PropertyDescriptor)
            {
                case PropertyDescriptor pd:
                {
                    if (pd.Attributes[typeof(DisplayAttribute)] is DisplayAttribute attr)
                    {
                        dispAttr = attr;
                    }

                    break;
                }
                case PropertyInfo pi:
                {
                    var attr = pi.GetCustomAttributes().OfType<DisplayAttribute>().FirstOrDefault();
                    if (attr != null)
                    {
                        dispAttr = attr;
                    }

                    break;
                }
            }

            if (dispAttr != null)
            {
                //var col = new DataGridTemplateColumn
                //{
                //    Header = $"{dispAttr.Order}. {dispAttr.Name}",
                //    //CellTemplateSelector = _cellTempSelector
                //};

                e.Column.Header = $"{dispAttr.Order}. {dispAttr.Name}";
            }
        }
    }
}
