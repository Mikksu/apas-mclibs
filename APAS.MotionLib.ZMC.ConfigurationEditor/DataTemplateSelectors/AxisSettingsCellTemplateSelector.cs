using System.Windows;
using System.Windows.Controls;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.DataTemplateSelectors
{
    internal class AxisSettingsCellTemplateSelector : DataTemplateSelector
    {
        internal DataTemplate DefaultCellTemplate { get; set; }

        internal DataTemplate DiSourceEditor { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DataGridTemplateColumn col)
            {


                return DefaultCellTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
