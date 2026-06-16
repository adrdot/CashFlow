using System.Security.Cryptography;
using System.Text;

namespace CashFlow.Transactions.Infrastructure.EventStore;

internal static class IdempotencyEventId
{
    private static readonly Guid Namespace = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

    public static Guid Create(string userId, string idempotencyKey)
    {
        var normalized = $"{userId.Trim()}:{idempotencyKey.Trim()}";
        return CreateUuidV5(Namespace, normalized);
    }

    private static Guid CreateUuidV5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(buffer, 0);
        nameBytes.CopyTo(buffer, namespaceBytes.Length);

        var hash = SHA1.HashData(buffer);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        SwapGuidByteOrder(hash);

        return new Guid(hash[..16]);
    }

    private static void SwapGuidByteOrder(Span<byte> guidBytes)
    {
        (guidBytes[0], guidBytes[3]) = (guidBytes[3], guidBytes[0]);
        (guidBytes[1], guidBytes[2]) = (guidBytes[2], guidBytes[1]);
        (guidBytes[4], guidBytes[5]) = (guidBytes[5], guidBytes[4]);
        (guidBytes[6], guidBytes[7]) = (guidBytes[7], guidBytes[6]);
    }
}
