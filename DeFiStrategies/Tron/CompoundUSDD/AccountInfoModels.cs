namespace DeFi_Strategies.Tron.CompoundUSDD
{
    public class AccountInfoRoot
    {
        public Datum[] data { get; set; }
        public bool success { get; set; }
        public Meta meta { get; set; }
    }

    public class Meta
    {
        public long at { get; set; }
        public int page_size { get; set; }
    }

    public class Datum
    {
        public long latest_opration_time { get; set; }
        public Owner_Permission owner_permission { get; set; }
        public Free_Asset_Net_Usagev2[] free_asset_net_usageV2 { get; set; }
        public int free_net_usage { get; set; }
        public Account_Resource account_resource { get; set; }
        public Active_Permission[] active_permission { get; set; }
        public Assetv2[] assetV2 { get; set; }
        public string address { get; set; }
        public long balance { get; set; }
        public long create_time { get; set; }
        public Trc20[] trc20 { get; set; }
        public long latest_consume_free_time { get; set; }
    }

    public class Owner_Permission
    {
        public Key[] keys { get; set; }
        public long threshold { get; set; }
        public string permission_name { get; set; }
    }

    public class Key
    {
        public string address { get; set; }
        public long weight { get; set; }
    }

    public class Account_Resource
    {
        public long latest_consume_time_for_energy { get; set; }
    }

    public class Free_Asset_Net_Usagev2
    {
        public long value { get; set; }
        public string key { get; set; }
    }

    public class Active_Permission
    {
        public string operations { get; set; }
        public Key1[] keys { get; set; }
        public long threshold { get; set; }
        public long id { get; set; }
        public string type { get; set; }
        public string permission_name { get; set; }
    }

    public class Key1
    {
        public string address { get; set; }
        public long weight { get; set; }
    }

    public class Assetv2
    {
        public long value { get; set; }
        public string key { get; set; }
    }

    public class Trc20
    {
        public string TPYmHEhy5n8TCEfYGqW2rPxsghSfzghPDn { get; set; }
        public string TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t { get; set; }
        public string TNLcz8A9hGKbTNJ6b6C1GTyigwxURbWzkM { get; set; }
    }
}
