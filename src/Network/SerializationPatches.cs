using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using IncreasePlayerLimit.Configuration;

namespace IncreasePlayerLimit.Network;

/// <summary>
/// Cache of PacketWriter and PacketReader reflection targets for transpilers.
/// </summary>
internal static class SerializationMethods
{
    internal static readonly MethodInfo? WriteIntWithBits =
        AccessTools.Method(typeof(PacketWriter), nameof(PacketWriter.WriteInt), new[] { typeof(int), typeof(int) });

    internal static readonly MethodInfo? ReadIntWithBits =
        AccessTools.Method(typeof(PacketReader), nameof(PacketReader.ReadInt), new[] { typeof(int) });

    internal static readonly MethodInfo? WriteListWithBits =
        typeof(PacketWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == nameof(PacketWriter.WriteList)
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType == typeof(int));

    internal static readonly MethodInfo? ReadListWithBits =
        typeof(PacketReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == nameof(PacketReader.ReadList)
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(int));
}

// --- StartRunLobbyPlayer SlotId Patches (2 bits -> 4 bits) ---

[HarmonyPatch(typeof(StartRunLobbyPlayer), nameof(StartRunLobbyPlayer.Serialize))]
internal static class StartRunLobbyPlayerSerializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.WriteIntWithBits,
            ModConfig.VanillaSlotIdBits, ModConfig.SlotIdBits,
            nameof(StartRunLobbyPlayerSerializePatch));
}

[HarmonyPatch(typeof(StartRunLobbyPlayer), nameof(StartRunLobbyPlayer.Deserialize))]
internal static class StartRunLobbyPlayerDeserializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.ReadIntWithBits,
            ModConfig.VanillaSlotIdBits, ModConfig.SlotIdBits,
            nameof(StartRunLobbyPlayerDeserializePatch));
}

// --- ClientLobbyJoinResponseMessage Patches (3 bits -> 5 bits) ---

[HarmonyPatch(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Serialize))]
internal static class ClientLobbyJoinResponseSerializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.WriteListWithBits,
            ModConfig.VanillaLobbyListLengthBits, ModConfig.LobbyListLengthBits,
            nameof(ClientLobbyJoinResponseSerializePatch));
}

[HarmonyPatch(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Deserialize))]
internal static class ClientLobbyJoinResponseDeserializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.ReadListWithBits,
            ModConfig.VanillaLobbyListLengthBits, ModConfig.LobbyListLengthBits,
            nameof(ClientLobbyJoinResponseDeserializePatch));
}

// --- LobbyBeginRunMessage Patches (3 bits -> 5 bits) ---

[HarmonyPatch(typeof(LobbyBeginRunMessage), nameof(LobbyBeginRunMessage.Serialize))]
internal static class LobbyBeginRunSerializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.WriteListWithBits,
            ModConfig.VanillaLobbyListLengthBits, ModConfig.LobbyListLengthBits,
            nameof(LobbyBeginRunSerializePatch));
}

[HarmonyPatch(typeof(LobbyBeginRunMessage), nameof(LobbyBeginRunMessage.Deserialize))]
internal static class LobbyBeginRunDeserializePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => TranspilerUtils.ReplaceBitWidthBeforeCall(instructions,
            SerializationMethods.ReadListWithBits,
            ModConfig.VanillaLobbyListLengthBits, ModConfig.LobbyListLengthBits,
            nameof(LobbyBeginRunDeserializePatch));
}
