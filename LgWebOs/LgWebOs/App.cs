namespace LgWebOs
{
    public class App
    {
        public string id { get; set; }
        public string title { get; set; }
        public string icon { get; set; }

        public override string ToString()
        {
            return id + ":" + title + ":" + icon;
        }
    }
}