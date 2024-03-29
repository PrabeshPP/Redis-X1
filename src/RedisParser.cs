public static class RedisParser
{
    public static List<string> RedisReqParser(string request)
    {
        string[] strList = request.Split("\r\n");
        if (strList[0][0] != '*')
        {
            throw new Exception("Error in Encoded Message");
        }

        int length = int.Parse(strList[0][1..]);
        return strList.Skip(1).Take(length * 2).Where(x => !x.StartsWith("$")).ToList();
    }

}