using System.Collections.Generic;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Core
{
    internal class AxisSettingsCollection : List<AxisSettings>
    {
        #region Constructors
        
        public AxisSettingsCollection()
        {
            
        }

        public AxisSettingsCollection(int capacity) : base(capacity)
        {
            
        }

        public AxisSettingsCollection(IEnumerable<AxisSettings> source) : base(source)
        {
            
        }
        
        #endregion
    }
}
