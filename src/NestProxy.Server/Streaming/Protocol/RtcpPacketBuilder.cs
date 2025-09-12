using System.Text;

namespace NestProxy.Server.Streaming.Protocol;

/// <summary>
/// Provides static methods for constructing minimal RTCP compound packets, including Receiver Report (RR), Source
/// Description (SDES) with CNAME, and BYE packets, according to RFC 3550.
/// </summary>
public static class RtcpPacketBuilder
{
    // Computes the exact size, in bytes, required to write an RTCP compound
    // packet consisting of a minimal RR and an SDES CNAME item for a given CNAME length.
    public static int ComputeCompoundRrWithSdesLength(int cnameLength)
    {
        // RR is a constant 8 bytes.
        const int rrLen = 8;
        // SDES packet length depends on cname length and padding to 32-bit boundary.
        int itemsLen = 2 + cnameLength + 1; // item header(2) + cname + END(1)
        int chunkLen = 4 + itemsLen;        // SSRC(4) + items
        int pad = (4 - (chunkLen & 3)) & 3; // pad to 32-bit boundary
        int sdesLen = 4 + chunkLen + pad;   // header(4) + chunk + pad
        return rrLen + sdesLen;
    }

    // Writes a compound RTCP packet (RR + SDES CNAME) into destination span.
    // Returns true on success and sets 'written' to the number of bytes written.
    public static bool TryWriteCompoundRrWithSdes(Span<byte> destination, uint ssrc, ReadOnlySpan<byte> cname, out int written)
    {
        int needed = ComputeCompoundRrWithSdesLength(cname.Length);
        if (destination.Length < needed)
        {
            written = 0;
            return false;
        }

        // RR (8 bytes)
        // Header
        destination[0] = 0x80; // V=2, P=0, RC=0
        destination[1] = 201;  // PT=RR
        destination[2] = 0x00;
        destination[3] = 0x01; // length=1 (32-bit words minus one)
        // SSRC
        destination[4] = (byte)(ssrc >> 24);
        destination[5] = (byte)(ssrc >> 16);
        destination[6] = (byte)(ssrc >> 8);
        destination[7] = (byte)ssrc;

        // SDES header offset
        int offset = 8;
        // SDES header
        destination[offset + 0] = 0x81; // V=2, SC=1
        destination[offset + 1] = 202;  // PT=SDES
        // We will fill length after computing SDES size
        // SDES SSRC
        int sdesHeaderLen = 4;
        // Compute chunk and padding
        int itemsLen = 2 + cname.Length + 1; // item header(2) + cname + END(1)
        int chunkLen = 4 + itemsLen;         // SSRC(4) + items
        int pad = (4 - (chunkLen & 3)) & 3;  // pad to 32-bit boundary
        int sdesLen = sdesHeaderLen + chunkLen + pad;
        ushort wordsMinus1 = (ushort)((sdesLen / 4) - 1);
        destination[offset + 2] = (byte)(wordsMinus1 >> 8);
        destination[offset + 3] = (byte)(wordsMinus1 & 0xFF);

        // SDES SSRC
        destination[offset + 4] = (byte)(ssrc >> 24);
        destination[offset + 5] = (byte)(ssrc >> 16);
        destination[offset + 6] = (byte)(ssrc >> 8);
        destination[offset + 7] = (byte)ssrc;

        int idx = offset + 8;
        destination[idx++] = 0x01; // CNAME
        destination[idx++] = (byte)cname.Length;
        cname.CopyTo(destination[idx..]);
        idx += cname.Length;
        destination[idx++] = 0x00; // END
        // Padding bytes (if any) are implicitly zero, Span is already allocated; no need to set.

        written = needed;
        return true;
    }
    public static byte[] BuildCompoundRrWithSdes(uint ssrc, string cname)
    {
        // RTCP Receiver Report (RR) packet (RFC 3550):
        //    0               1               2               3
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //    |V=2|P|   RC=0  |   PT=201(RR)  |       length  |
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //    |                         SSRC                   |
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        // For a minimal RR with RC=0, length = 1 (32-bit words minus one).
        byte[] rr =
        [
            0x80, // V=2, P=0, RC=0
            201,  // PT=RR
            0x00,
            0x01, // length=1
            (byte)(ssrc >> 24),
            (byte)(ssrc >> 16),
            (byte)(ssrc >> 8),
            (byte)ssrc,
        ];

        // RTCP SDES (Source Description) packet with a single chunk and CNAME item:
        //    0               1               2               3
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //    |V=2|P|  SC=1   |  PT=202(SDES) |       length  |
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //    |                         SSRC                   |
        //    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //    | CNAME=1 | length | cname bytes ... | END=0 | pad ... |
        // length counts 32-bit words minus one for the whole SDES packet.
        byte[] cnameBytes = Encoding.ASCII.GetBytes(cname);
        int itemsLen = 2 + cnameBytes.Length + 1; // item header(2) + cname + END(1)
        int chunkLen = 4 + itemsLen;              // SSRC(4) + items
        int pad = (4 - (chunkLen & 3)) & 3;       // pad to 32-bit boundary
        int sdesLen = 4 + chunkLen + pad;         // header(4) + chunk + pad
        byte[] sdes = new byte[sdesLen];
        sdes[0] = 0x81; // V=2, SC=1
        sdes[1] = 202;  // PT=SDES
        ushort wordsMinus1 = (ushort)((sdesLen / 4) - 1);
        sdes[2] = (byte)((wordsMinus1 >> 8) & 0xFF);
        sdes[3] = (byte)(wordsMinus1 & 0xFF);
        sdes[4] = (byte)(ssrc >> 24);
        sdes[5] = (byte)(ssrc >> 16);
        sdes[6] = (byte)(ssrc >> 8);
        sdes[7] = (byte)ssrc;
        int idx = 8;
        sdes[idx++] = 0x01; // CNAME
        sdes[idx++] = (byte)cnameBytes.Length;
        // Copy CNAME bytes without extra allocations. Buffer.BlockCopy is efficient for byte[]
        Buffer.BlockCopy(cnameBytes, 0, sdes, idx, cnameBytes.Length);
        idx += cnameBytes.Length;
        sdes[idx++] = 0x00; // END
                            // Zero-filled bytes after END act as padding. Array initialization ensures zeroes.

        byte[] compound = new byte[rr.Length + sdes.Length];
        // Concatenate RR and SDES to form a compound RTCP packet as keepalive
        Buffer.BlockCopy(rr, 0, compound, 0, rr.Length);
        Buffer.BlockCopy(sdes, 0, compound, rr.Length, sdes.Length);
        return compound;
    }

    /// <summary>
    /// Builds a minimal standalone RTCP BYE packet (single SSRC, no reason string).
    /// RFC 3550 allows a BYE-only compound; header length = (SSRC list (4) / 4) - 1 = 1 - 1 = 0.
    /// </summary>
    /// <param name="ssrc">SSRC to signal termination for.</param>
    /// <returns>Byte array containing the BYE packet.</returns>
    public static byte[] BuildBye(uint ssrc)
    {
        // BYE header (4 bytes) + SSRC (4 bytes) = 8 bytes total.
        byte[] bye = new byte[8];
        bye[0] = 0x80; // V=2, P=0, SC=0 (one SSRC in this context represented implicitly by RC=1? Actually RC=1 => lower 5 bits). Use RC=1.
        bye[0] = (byte)(0x80 | 0x01); // Set RC=1 for one SSRC.
        bye[1] = 203;  // PT=BYE
        // length in 32-bit words minus one -> (8/4)-1 = 1
        bye[2] = 0x00;
        bye[3] = 0x01;
        bye[4] = (byte)(ssrc >> 24);
        bye[5] = (byte)(ssrc >> 16);
        bye[6] = (byte)(ssrc >> 8);
        bye[7] = (byte)ssrc;
        return bye;
    }
}