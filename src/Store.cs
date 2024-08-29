namespace codecrafters_redis;

public class Store
{
    private Dictionary<string, Value> map;

    public Store()
    {
        map = new Dictionary<string, Value>();
    }


    public string Set(string[] command, DateTime currTime)
    {
        try
        {
            int pxFlag = command.ToList().IndexOf("px");
            if (command.Length == 3)
            {
                DateTime expiry = DateTime.MaxValue;
                Value val = new Value(command[2], currTime, expiry);
                map[command[1]] = val;
            }
            else if (pxFlag >-1)// && command.Length == 5 && command[3].Equals("px")
            {
                int delta = int.Parse(command[pxFlag+1]);

                DateTime expiry = currTime.AddMilliseconds(delta);
                Value val = new Value(command[2], currTime, expiry);
                map[command[1]] = val;
            }
            return "+OK\r\n";
        }
        catch (Exception)
        {
            return $"$-1\r\n";
        }
    }

    public string Get(string[] command, DateTime currTime)
    {
        try
        {
            Value val = map[command[1]];

            if (currTime <= val.expiry)
            {
                return $"+{val.val}\r\n";
            }
            else
            {
                map.Remove(command[1]);
                return $"$-1\r\n";
            }
        }
        catch (Exception)
        {
            return $"$-1\r\n";
        }
        
    }
}

public class Value {
    public string val;
    public DateTime created;
    public DateTime expiry;
    public Value(string val, DateTime created, DateTime expiry)
    {
        this.val = val;
        this.created = created;
        this.expiry = expiry;
    }

}