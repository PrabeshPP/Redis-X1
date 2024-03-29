//defining struct for the expiry time
public class RedisExpiryModel
{
    public string Value { get; set; }
    public DateTime? Expiry { get; set; }

    public RedisExpiryModel(string value, DateTime? expiry)
    {
        Value = value;
        Expiry = expiry;
    }
}
