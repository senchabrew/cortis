// =============================================================================
// ExamplePresenter.cs
// [ProtoHandler] を使ったPresenterの実装例（Command+Event / ルーティングなし）
// =============================================================================
//
// 前提: protobuf で以下のようなメッセージを定義済み
//
//   message Cube {
//     message Command {
//       message SetScale { float x = 1; float y = 2; float z = 3; }
//       message Reset {}
//       oneof command {
//         SetScale set_scale = 1;
//         Reset reset = 2;
//       }
//     }
//     message Event {
//       message ScaleChanged { float x = 1; float y = 2; float z = 3; }
//       oneof event {
//         ScaleChanged scale_changed = 1;
//       }
//     }
//   }
//
// ルーティングなしの場合: Gateway が Cube.Command / Cube.Event を直接やり取りする。
// root 型 (App.Command) に包まれている場合は RoutedPresenter.cs を参照。
// =============================================================================

using Cortis;
using UnityEngine;

namespace Example
{
    // [ProtoHandler] を付けると Source Generator が以下を自動生成する:
    //   - ICommandHandler<Cube.Types.Command>.Handle() の switch ディスパッチ
    //   - IEventSource<Cube.Types.Event>.Events プロパティ
    //   - DispatchEvent() ヘルパーメソッド（各 event case ごと）
    //   - IInitializable.Initialize() / IDisposable.Dispose()
    //   - Register() 静的メソッド（VContainer 登録用）
    [ProtoHandler(typeof(Cube.Types.Command), typeof(Cube.Types.Event))]
    public sealed partial class ExamplePresenter
    {
        readonly Transform _target;

        public ExamplePresenter(Transform target)
        {
            _target = target;
        }

        // ハンドラ: メソッド引数の型で自動マッチされる
        void HandleSetScale(Cube.Types.Command.Types.SetScale cmd)
        {
            var scale = new Vector3(cmd.X, cmd.Y, cmd.Z);
            _target.localScale = scale;

            // 状態変更を Flutter 側に通知
            DispatchEvent(new Cube.Types.Event.Types.ScaleChanged
            {
                X = scale.x,
                Y = scale.y,
                Z = scale.z,
            });
        }

        void HandleReset(Cube.Types.Command.Types.Reset cmd)
        {
            _target.localScale = Vector3.one;

            DispatchEvent(new Cube.Types.Event.Types.ScaleChanged
            {
                X = 1f,
                Y = 1f,
                Z = 1f,
            });
        }

        // Source Generator が呼び出す partial メソッド
        private partial void OnInitialize()
        {
            Debug.Log("ExamplePresenter initialized");
        }

        private partial void OnDispose()
        {
            // クリーンアップ処理
        }
    }
}
