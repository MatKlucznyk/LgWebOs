namespace LgWebOs
{
    public class ExternalInput
    {
        public string id { get; set; }
        public string label { get; set; }
        public string icon { get; set; }

        public override string ToString()
        {
            return id + ":" + label + ":" + icon;
        }
    }
}