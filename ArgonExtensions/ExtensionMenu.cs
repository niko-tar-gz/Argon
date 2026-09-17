using System;
using System.Collections.Generic;
using System.Linq;
using Argon;
using BepInEx.Configuration;
using GorillaNetworking;
using UnityEngine;

namespace ArgonExtensions
{
    internal sealed class ExtensionMenu : MonoBehaviour
    {
        private const char HistorySeparator = '|';
        private const string GrayTag = "<color=#7f7f7f>";
        private ConfigEntry<string> mutedHandTaps;
        private ConfigEntry<string> roomHistory;
        private readonly HashSet<string> handTapMutes = new HashSet<string>();
        private readonly HashSet<string> reported = new HashSet<string>();
        private readonly List<string> rooms = new List<string>();

        internal void Initialize(ConfigFile config)
        {
            mutedHandTaps = config.Bind("Players", "Muted Hand Taps", "", "Player IDs whose hand taps are locally muted.");
            roomHistory = config.Bind("Rooms", "History", "", "Previously joined room codes.");
            Load(mutedHandTaps.Value, handTapMutes);
            rooms.AddRange((roomHistory.Value ?? "").Split(new[] { HistorySeparator }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            MenuApi.RegisterRootItem("Extensions", CreateRootPage);
        }

        private bool subscribed;

        private void Update()
        {
            NetworkSystem network = NetworkSystem.Instance;
            if (!subscribed && network != null)
            {
                subscribed = true;
                network.OnJoinedRoomEvent += RecordCurrentRoom;
                RecordCurrentRoom();
            }
        }

        private void OnDestroy()
        {
            if (NetworkSystem.Instance != null && subscribed) NetworkSystem.Instance.OnJoinedRoomEvent -= RecordCurrentRoom;
        }

        private MenuPage CreateRootPage() => new MenuPage("Argon Extensions", new[]
        {
            new MenuItem("Disconnect", CreateDisconnectConfirmPage),
            new MenuItem("Lobby Hop", CreateLobbyHopConfirmPage),
            new MenuItem("Player List", CreatePlayerListPage),
            new MenuItem("Room History", CreateRoomHistoryPage)
        });

        private MenuPage CreateDisconnectConfirmPage() => new MenuPage("Disconnect?", new[]
        {
            new MenuItem("Yes, disconnect", Disconnect, true),
            new MenuItem("No", (Action)null, true)
        });

        private void Disconnect()
        {
            if (NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom)
                _ = NetworkSystem.Instance.ReturnToSinglePlayer();
        }

        private MenuPage CreateLobbyHopConfirmPage() => new MenuPage("Lobbyhop?", new[]
        {
            new MenuItem("Yes, hop to another room", LobbyHop, true),
            new MenuItem("No", (Action)null, true)
        });

        private void LobbyHop()
        {
            NetworkSystem network = NetworkSystem.Instance;
            GorillaNetworkJoinTrigger trigger = PhotonNetworkController.Instance != null ? PhotonNetworkController.Instance.currentJoinTrigger : null;
            if (network == null || !network.InRoom || trigger == null) return;
            _ = network.ReturnToSinglePlayer();
            StartCoroutine(LobbyHopJoin(trigger));
        }

        private System.Collections.IEnumerator LobbyHopJoin(GorillaNetworkJoinTrigger trigger)
        {
            const float timeout = 60f;
            float start = Time.time;
            yield return new WaitUntil(() => NetworkSystem.Instance != null && NetworkSystem.Instance.netState == NetSystemState.Idle || Time.time - start > timeout);
            NetworkSystem network = NetworkSystem.Instance;
            if (network != null && network.netState == NetSystemState.Idle && PhotonNetworkController.Instance != null)
                PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger, JoinType.Solo);
        }

        private MenuPage CreatePlayerListPage()
        {
            IEnumerable<NetPlayer> players = NetworkSystem.Instance == null
                ? Enumerable.Empty<NetPlayer>() : NetworkSystem.Instance.PlayerListOthers;
            List<MenuItem> items = players.OrderBy(player => player.NickName).Select(player =>
            {
                return new MenuItem(player.NickName, () => CreatePlayerPage(player));
            }).ToList();
            if (items.Count == 0) items.Add(new MenuItem("No other players in this room.", (Action)null));
            return new MenuPage("Player List", items);
        }

        private MenuPage CreatePlayerPage(NetPlayer player)
        {
            bool isTapMuted = handTapMutes.Contains(player.UserId);
            return new MenuPage(player.NickName, new[]
            {
                new MenuItem(IsMuted(player) ? "Unmute" : "Mute", () => ToggleMute(player)),
                new MenuItem("Report...", () => CreateReportPage(player)),
                new MenuItem(isTapMuted ? "Unmute hand taps" : "Mute hand taps", () => ToggleHandTaps(player))
            });
        }

        private MenuPage CreateReportPage(NetPlayer player)
        {
            List<MenuItem> items = new List<MenuItem>
            {
                ReportEntry(player, "Hate speech", GorillaPlayerLineButton.ButtonType.HateSpeech),
                ReportEntry(player, "Cheating", GorillaPlayerLineButton.ButtonType.Cheating),
                ReportEntry(player, "Toxicity", GorillaPlayerLineButton.ButtonType.Toxicity)
            };
            return new MenuPage("Report " + player.NickName, items);
        }

        private MenuItem ReportEntry(NetPlayer player, string label, GorillaPlayerLineButton.ButtonType reason)
        {
            string key = Key(player.UserId, reason);
            if (reported.Contains(key))
            {
                return new MenuItem(GrayTag + label + " (reported)</color>", (Action)null, false, true);
            }
            return new MenuItem(label, () =>
            {
                GorillaPlayerScoreboardLine.ReportPlayer(player.UserId, reason, player.NickName);
                reported.Add(key);
            }, true);
        }

        private void ToggleMute(NetPlayer player)
        {
            RigContainer rig = FindRig(player);
            if (rig == null) return;
            bool mute = !rig.IsMutedFor(RigContainer.MuteReason.Manual);
            rig.hasManualMute = mute;
            rig.SetMuted(RigContainer.MuteReason.Manual, mute);
            PlayerPrefs.SetInt(player.UserId, mute ? 1 : 0);
            PlayerPrefs.Save();
            GorillaScoreboardTotalUpdater.ReportMute(player, mute ? 1 : 0);
        }

        private static bool IsMuted(NetPlayer player)
        {
            RigContainer rig = FindRig(player);
            return rig != null && rig.IsMutedFor(RigContainer.MuteReason.Manual);
        }

        private static RigContainer FindRig(NetPlayer player)
        {
            if (player == null) return null;
            return VRRigCache.ActiveRigContainers.FirstOrDefault(rig => rig != null && rig.Creator != null && rig.Creator.UserId == player.UserId);
        }

        private void ToggleHandTaps(NetPlayer player) { Toggle(handTapMutes, player.UserId); Save(mutedHandTaps, handTapMutes); }
        private static void Toggle(HashSet<string> set, string id) { if (!set.Add(id)) set.Remove(id); }

        private MenuPage CreateRoomHistoryPage()
        {
            List<MenuItem> items = rooms.AsEnumerable().Reverse().Select(room => new MenuItem(room, () => JoinRoom(room), true)).ToList();
            if (items.Count == 0) items.Add(new MenuItem("woah, so empty,,,,", (Action)null));
            return new MenuPage("Room History", items);
        }

        private static void JoinRoom(string room)
        {
            if (PhotonNetworkController.Instance != null)
                PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(room, JoinType.Solo);
        }

        private void RecordCurrentRoom()
        {
            string room = NetworkSystem.Instance?.RoomName;
            if (string.IsNullOrEmpty(room)) return;
            rooms.Remove(room);
            rooms.Add(room);
            if (rooms.Count > 20) rooms.RemoveAt(0);
            roomHistory.Value = string.Join(HistorySeparator.ToString(), rooms);
        }

        private static string Key(string userId, GorillaPlayerLineButton.ButtonType reason) => userId + "|" + reason;

        internal static bool IsHandTapMuted(NetPlayer player) => Instance != null && player != null && Instance.handTapMutes.Contains(player.UserId);
        private static ExtensionMenu Instance { get; set; }
        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private static void Load(string value, ISet<string> destination)
        {
            foreach (string id in (value ?? "").Split(new[] { HistorySeparator }, StringSplitOptions.RemoveEmptyEntries)) destination.Add(id);
        }
        private static void Save(ConfigEntry<string> entry, IEnumerable<string> values) => entry.Value = string.Join(HistorySeparator.ToString(), values);
    }
}
