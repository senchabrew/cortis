// =============================================================================
// ExamplePresenter.cs
// [ProtoHandler] を使ったPresenterの実装例
// =============================================================================
//
// 前提: protobuf で以下のようなメッセージを定義済み
//
//   message ExampleCommand {
//     oneof command {
//       SetScale set_scale = 1;
//       Reset reset = 2;
//     }
//     message SetScale { float x = 1; float y = 2; float z = 3; }
//     message Reset {}
//   }
//
//   message ExampleEvent {
//     oneof event {
//       ScaleChanged scale_changed = 1;
//     }
//     message ScaleChanged { float x = 1; float y = 2; float z = 3; }
//   }
// =============================================================================

using Cortis;
using UnityEngine;

namespace Example
{
    // [ProtoHandler] を付けると Source Generator が以下を自動生成する:
    //   - ICommandHandler<ExampleCommand>.Handle() の switch ディスパッチ
    //   - IEventSource<ExampleEvent>.Events プロパティ
    //   - DispatchEvent() ヘルパーメソッド（各 Event case ごと）
    //   - IInitializable.Initialize() / IDisposable.Dispose()
    //   - Register() 静的メソッド（VContainer 登録用）
    [ProtoHandler(typeof(ExampleCommand), typeof(ExampleEvent))]
    public sealed partial class ExamplePresenter
    {
        readonly Transform _target;

        public ExamplePresenter(Transform target)
        {
            _target = target;
        }

        // コマンドハンドラ: メソッド引数の型で自動マッチされる
        void HandleSetScale(ExampleCommand.Types.SetScale cmd)
        {
            var scale = new Vector3(cmd.X, cmd.Y, cmd.Z);
            _target.localScale = scale;

            // イベントを Flutter 側に通知
            DispatchEvent(new ExampleEvent.Types.ScaleChanged
            {
                X = scale.x,
                Y = scale.y,
                Z = scale.z,
            });
        }

        void HandleReset(ExampleCommand.Types.Reset cmd)
        {
            _target.localScale = Vector3.one;

            DispatchEvent(new ExampleEvent.Types.ScaleChanged
            {
                X = 1f,
                Y = 1f,
                Z = 1f,
            });
        }

        // Source Generator が呼び出す partial メソッド
        private partial void OnInitialize()
        {
            // Binder 接続後の初期化処理
            Debug.Log("ExamplePresenter initialized");
        }

        private partial void OnDispose()
        {
            // クリーンアップ処理
        }
    }
}
