using Avg.ModuleFramework.Events;
using Avg.ModuleFramework.Simpl.XSig;

namespace LgWebOs.Events
{
    public class StringArrayEventArgs : GenericEventArgs<string[]>
    {

        public ushort Count
        {
            get
            {
                return (ushort)(Payload != null ? Payload.Length : 0);
            }
        }

        public string[] XSigValues { get; private set; }

        public StringArrayEventArgs() : base()
        {
        }

        public StringArrayEventArgs(string[] value) : base(value)
        {
            XSigValues = new string[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                XSigValues[i] = XSigHelpers.GetString(i + 1, value[i]);
            }
        }
    }
}
