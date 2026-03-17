// =============================================================================
// RoutedPresenter.cs
// [ProtoHandler] のルーティングパターン実装例
// =============================================================================
//
// 前提: protobuf で以下のようなメッセージを定義済み
//
//   // フィーチャー: Player
//   message Player {
//     message Command {
//       message Attack { int32 damage = 1; }
//       message Defend { int32 shield = 1; }
//       oneof command {
//         Attack attack = 1;
//         Defend defend = 2;
//       }
//     }
//     message Event {
//       message HealthChanged { int32 hp = 1; }
//       oneof event {
//         HealthChanged health_changed = 1;
//       }
//     }
//   }
//
//   // ルート: App（各フィーチャーの Command / Event を oneof で包む）
//   message App {
//     message Command {
//       oneof command {
//         Cube.Command cube = 1;
//         Player.Command player = 2;
//       }
//     }
//     message Event {
//       oneof event {
//         Cube.Event cube = 1;
//         Player.Event player = 2;
//       }
//     }
//   }
//
// Gateway は App.Command / App.Event で通信する。
// [ProtoHandler] に inner 型 (Player.Command / Player.Event) を指定すると、
// Source Generator が root 型 (App.Command / App.Event) を自動発見し、
// unwrap / wrap コードを生成する。
// =============================================================================

using Cortis;
using UnityEngine;

namespace Example
{
    // ルーティング: [ProtoHandler] に inner 型を指定すると、
    // Source Generator が root 型を自動発見し、以下を生成する:
    //
    //   _binder = MessageBinding.BindRouted<
    //       App.Types.Command,    // root command
    //       Player.Types.Command, // inner command
    //       App.Types.Event,      // root event
    //       Player.Types.Event    // inner event
    //   >(
    //       this, this, gateway,
    //       root => root.CommandCase == App.Types.Command.CommandOneofCase.Player
    //           ? root.Player : null,
    //       inner => new App.Types.Event { Player = inner }
    //   );
    //
    // Presenter のコードは非ルーティング時と同じ書き方。
    // root 型を意識する必要はない。
    [ProtoHandler(typeof(Player.Types.Command), typeof(Player.Types.Event))]
    public sealed partial class RoutedPresenter
    {
        int _hp = 100;

        void HandleAttack(Player.Types.Command.Types.Attack cmd)
        {
            _hp -= cmd.Damage;
            Debug.Log($"Attacked! HP: {_hp}");

            DispatchEvent(new Player.Types.Event.Types.HealthChanged { Hp = _hp });
        }

        void HandleDefend(Player.Types.Command.Types.Defend cmd)
        {
            Debug.Log($"Defending with shield: {cmd.Shield}");
        }

        private partial void OnInitialize()
        {
            Debug.Log("RoutedPresenter initialized");
        }

        private partial void OnDispose() { }
    }
}
