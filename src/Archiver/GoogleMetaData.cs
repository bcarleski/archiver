namespace Archiver
{
    class GoogleMetaData
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public GoogleDate CreationTime { get; set; }
        public GoogleDate PhotoTakenTime { get; set; }
        public GoogleGeoData GeoData { get; set; }
        public GoogleGeoData GeoDataExif { get; set; }
        public GooglePerson[] People { get; set; }
    }
}
