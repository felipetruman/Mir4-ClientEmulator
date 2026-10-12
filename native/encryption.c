#include <windows.h>
#include <stdint.h>
#include <string.h>
#include "current_parameters.h"

/* Packet layout, checksum and timestamp follow AlSch092/Mir4-PacketEncryption
 * Encryption.cpp and Functions.asm (33dde13db9f5cc7c4850b6647cc2131f13bd9d67).
 * Current client changes: generated key schedule/table, bounded padding buffer,
 * current checksum variants and a per-send counter instead of a fixed value.
 */

#define API __declspec(dllexport)
#define MAX_INPUT (8192 - 5)
static volatile LONG64 session_counter = 0;

static uint8_t xt(uint8_t b) { return (uint8_t)((b << 1) ^ ((b & 128) ? 0x1b : 0)); }

API int __cdecl CipherExpandKey(const uint8_t *key, uint8_t *expanded) {
    if (!key || !expanded) return 0;
    memcpy(expanded, key, 16);
    for (int word = 4; word < 44; ++word) {
        uint8_t t[4]; memcpy(t, expanded + 4 * (word - 1), 4);
        if (!(word % 4)) {
            uint8_t first = t[0];
            t[0] = sbox[t[1]] ^ rcon[word / 4];
            t[1] = sbox[t[2]]; t[2] = sbox[t[3]]; t[3] = sbox[first];
        }
        for (int j = 0; j < 4; ++j) expanded[4 * word + j] = expanded[4 * (word - 4) + j] ^ t[j];
    }
    return 176;
}

API int __cdecl CipherEncryptBlock(const uint8_t *plain, uint8_t *out, const uint8_t *expanded) {
    if (!plain || !out || !expanded) return 0;
    uint8_t state[16];
    for (int i = 0; i < 16; ++i) state[i] = plain[i] ^ expanded[i];
    for (int round = 1; round <= 10; ++round) {
        uint8_t prev[16];
        for (int i = 0; i < 16; ++i) prev[i] = sbox[state[i]];
        for (int row = 0; row < 4; ++row)
            for (int col = 0; col < 4; ++col) state[col * 4 + row] = prev[((col + row) % 4) * 4 + row];
        if (round != 10) for (int col = 0; col < 4; ++col) {
            uint8_t b[4]; memcpy(b, state + col * 4, 4);
            uint8_t total = b[0] ^ b[1] ^ b[2] ^ b[3];
            for (int j = 0; j < 4; ++j) state[col * 4 + j] = b[j] ^ total ^ xt(b[j] ^ b[(j + 1) % 4]);
        }
        for (int i = 0; i < 16; ++i) state[i] ^= expanded[16 * round + i];
    }
    memcpy(out, state, 16); return 16;
}

// Caller supplies the header and timestamp. Capacity is checked before writing.
API int __cdecl CipherEncryptPrepared(const uint8_t *frame, uint8_t *out, int length, int capacity,
                                     const uint8_t *expanded, int variant) {
    if (!frame || !out || !expanded || length < 17 || length > 8192 || variant < 0 || variant > 1) return 0;
    int size = ((length + 14) & 0x00fffff0) + 1;
    if (capacity < size) return 0;
    memmove(out, frame, (size_t)length);
    memset(out + length, 4, (size_t)(size - length));
    CipherEncryptBlock(out, out, expanded);
    int checksum = out[15] - out[12] - out[9] - out[6] - out[3]
        + out[14] + out[13] + out[11] + out[10] + out[8] + out[7]
        + out[5] + out[4] + out[2] + out[1] + out[0];
    checksum = (checksum + (variant ? 0x49 : 0)) & 255;
    out[16] = (uint8_t)(checksum ? checksum : variant + 1);
    int previous = 0;
    for (int offset = 17; offset < size; offset += 16) {
        for (int i = 0; i < 16; ++i) out[offset + i] ^= out[previous + i];
        CipherEncryptBlock(out + offset, out + offset, expanded);
        previous = offset;
    }
    return size;
}

// Deterministic variant-0 entry point for regression tests and explicit callers.
API int __cdecl EncryptWithTicks(const uint8_t *input, uint8_t *out, int length, int capacity,
                                uint64_t unix_ticks_100ns, uint64_t counter, uint8_t flag) {
    if (!input || !out || length < 12 || length > MAX_INPUT) return 0;
    int raw_length = length + 5;
    int final_length = ((raw_length + 14) & 0x00fffff0) + 1;
    if (capacity < final_length) return 0;
    memmove(out + 5, input, (size_t)length);
    uint32_t wire_length = (uint32_t)raw_length;
    memcpy(out, &wire_length, 4); out[4] = flag;
    uint64_t stamp = (unix_ticks_100ns / 10000000) * 1257787 + counter;
    memcpy(out + 7, &stamp, 8);
    uint8_t expanded[176]; CipherExpandKey(current_key_variant0, expanded);
    return CipherEncryptPrepared(out, out, raw_length, capacity, expanded, 0);
}

API void __cdecl ResetCipherSession(uint64_t next_counter) {
    InterlockedExchange64(&session_counter, (LONG64)next_counter);
}

// Compatibility ABI: the existing managed caller allocates length + 30 bytes.
// It selects variant 0; authenticated acceptance still requires a live test.
API int __cdecl Encrypt(const uint8_t *input, uint8_t *out, int length) {
    if (!input || !out || length < 12 || length > MAX_INPUT) return 0;
    FILETIME ft; GetSystemTimePreciseAsFileTime(&ft);
    uint64_t ticks = (((uint64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime) - 116444736000000000ULL;
    uint64_t counter = (uint64_t)(InterlockedIncrement64(&session_counter) - 1);
    return EncryptWithTicks(input, out, length, length + 30, ticks, counter, current_header_flag);
}
