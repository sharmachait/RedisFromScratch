namespace codecrafters_redis;

public class ResponseDTO
{
    public string? response = null;
    public byte[]? data = null;

    public ResponseDTO(string response) 
    {
        this.response = response;
    }
    public ResponseDTO(string response, byte[] data)
    {
        this.data = data;
        this.response = response;
    }
}