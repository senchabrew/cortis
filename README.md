# Cortis

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Unity 6000.0+](https://img.shields.io/badge/Unity-6000.0%2B-black.svg)](#requirements)

Unity と外部プラットフォーム間の型安全な protobuf 通信を、最小限のボイラープレートで実現する Unity パッケージ。

## Features

- **Zero-boilerplate command dispatch** — `[ProtoHandler]` を付けるだけで、protobuf oneof → handler メソッドの switch ディスパッチを Source Generator が自動生成
- **Reactive event pipeline** — `SendEvent` ヘルパーを生成し、ユーザーが [R3](https://github.com/Cysharp/R3) の任意のオペレータで自由にパイプラインを構成できる
- **Auto-routing** — inner 型（例: `PlayerAction`）を指定するだけで、root 型（例: `AppAction`）までのルーティング経路をコンパイル時に自動発見し、unwrap/wrap コードを生成
- **Gateway abstraction** — `IMessageGateway` インターフェースにより、通信層の具体実装をパッケージから分離。Flutter、ネイティブアプリ、テスト環境など接続先を自由に差し替え可能
- **VContainer integration** — 生成される `Register()` メソッドで DI 登録を 1 行に集約

## Table of Contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Command-only / Event-only](#command-only--event-only)
- [Routing](#routing)
- [Architecture](#architecture)
- [Source Generator Diagnostics](#source-generator-diagnostics)
- [Development](#development)
- [License](#license)

## Requirements

- Unity 6000.0 or later
- [VContainer](https://github.com/hadashiA/VContainer)
- [R3](https://github.com/Cysharp/R3) (NuGet) + [R3.Unity](https://github.com/Cysharp/R3) (UPM)
- [Google.Protobuf](https://www.nuget.org/packages/Google.Protobuf/) (NuGet)
- [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity)

## Installation

### Step 1 — NuGet パッケージ

[NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity) をインストール後、**Window → NuGet → Manage NuGet Packages** から以下をインストール:

- `R3`
- `Google.Protobuf`

### Step 2 — UPM パッケージ

`Packages/manifest.json` の `dependencies` に追加:

```json
{
  "dependencies": {
    "com.cortis.core": "https://github.com/senchabrew/cortis.git?path=Packages/com.cortis.core",
    "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0",
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer"
  }
}
```

## Getting Started

### 1. protobuf メッセージを定義する

```protobuf
message MyCommand {
  oneof command {
    SetScale set_scale = 1;
    Reset reset = 2;
  }
  message SetScale { float x = 1; float y = 2; float z = 3; }
  message Reset {}
}

message MyEvent {
  oneof event {
    ScaleChanged scale_changed = 1;
  }
  message ScaleChanged { float x = 1; float y = 2; float z = 3; }
}
```

### 2. `[ProtoHandler]` で Handler を実装する

```csharp
using Cortis;
using R3;
using UnityEngine;

[ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
public sealed partial class MyHandler
{
    readonly Transform _target;
    readonly Subject<MyEvent> _events = new();

    public MyHandler(Transform target) => _target = target;

    // Handle + CaseName のメソッド名 & 引数の型で oneof case に自動マッチ
    void HandleSetScale(MyCommand.Types.SetScale cmd)
    {
        _target.localScale = new Vector3(cmd.X, cmd.Y, cmd.Z);
        _events.OnNext(new MyEvent { ScaleChanged = new() { X = cmd.X, Y = cmd.Y, Z = cmd.Z } });
    }

    void HandleReset(MyCommand.Types.Reset cmd)
    {
        _target.localScale = Vector3.one;
        _events.OnNext(new MyEvent { ScaleChanged = new() { X = 1, Y = 1, Z = 1 } });
    }

    private partial void OnInitialize()
    {
        _events.DistinctUntilChanged().Subscribe(evt => SendEvent(evt));
    }

    private partial void OnDispose()
    {
        _events.Dispose();
    }
}
```

<details>
<summary>Source Generator が自動生成するコード</summary>

- `ICommandHandler<MyCommand>.Handle()` — oneof case による switch ディスパッチ
- `SendEvent(MyEvent)` — Gateway へのイベント送信ヘルパー
- `Register(IContainerBuilder, Lifetime)` — VContainer 登録メソッド
- `Initialize()` / `Dispose()` — ライフサイクル管理

</details>

> `[ProtoHandler]` を付けたクラスは `partial` 必須・`Handle` プレフィックスが予約されるなど Cortis の規約に従う場所になる。アプリケーションロジックは別クラスに委譲し、Handler は oneof case をドメイン側のメソッドに繋ぐだけの薄い層に保つことを推奨する。

### 3. VContainer で登録する

```csharp
using Cortis;
using VContainer;
using VContainer.Unity;

public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Gateway: 利用側プロジェクトで IMessageGateway を実装する
        builder.Register<MyMessageGateway>(Lifetime.Scoped)
            .As<IMessageGateway>();

        // Handler と Binder をまとめて登録
        MyHandler.Register(builder, Lifetime.Scoped);
    }
}
```

### 4. `IMessageGateway` を実装する

Cortis は通信層を抽象化しているため、`IMessageGateway` の具体実装は利用側プロジェクトで提供する。

```csharp
public sealed class MyMessageGateway : IMessageGateway, IDisposable
{
    readonly Subject<Any> _messages = new();
    public Observable<Any> Messages => _messages;

    public void Send(Any packed)
    {
        // 外部プラットフォームへ送信
    }

    public void OnReceived(byte[] bytes)
    {
        try
        {
            _messages.OnNext(Any.Parser.ParseFrom(bytes));
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to parse message: {e}");
        }
    }

    public void Dispose() { _messages.OnCompleted(); _messages.Dispose(); }
}
```

> `Sample~/FlutterMessageGateway.cs` に FlutterUnityIntegration を使った実装例があります。

## Command-only / Event-only

### Command-only

イベントを発行せず、コマンド受信のみ行う場合は `[ProtoHandler]` の第2型引数を省略する:

```protobuf
message SpawnCommand {
  oneof command {
    Spawn spawn = 1;
  }
  message Spawn { string prefab_name = 1; }
}
```

```csharp
[ProtoHandler(typeof(SpawnCommand))]
public sealed partial class SpawnHandler
{
    void HandleSpawn(SpawnCommand.Types.Spawn cmd)
    {
        // コマンド処理のみ、イベント発行なし
    }

    private partial void OnInitialize() { }
    private partial void OnDispose() { }
}
```

> Command-only の場合、`SendEvent` は生成されず、`Binder<TCommand>` (1型引数版) が登録される。

### Event-only

コマンド受信なし、Unity 側からイベントを発行するだけの場合は第1引数を `null` にする:

```protobuf
message SensorEvent {
  oneof event {
    PositionUpdated position_updated = 1;
    StatusChanged status_changed = 2;
  }
  message PositionUpdated { float x = 1; float y = 2; float z = 3; }
  message StatusChanged { int32 status = 1; }
}
```

```csharp
[ProtoHandler(null, typeof(SensorEvent))]
public sealed partial class SensorHandler
{
    readonly Transform _sensor;
    readonly Subject<SensorEvent> _events = new();

    public SensorHandler(Transform sensor) => _sensor = sensor;

    void UpdatePosition()
    {
        var pos = _sensor.position;
        _events.OnNext(new SensorEvent { PositionUpdated = new() { X = pos.x, Y = pos.y, Z = pos.z } });
    }

    private partial void OnInitialize()
    {
        _events.DistinctUntilChanged().Subscribe(evt => SendEvent(evt));
    }

    private partial void OnDispose()
    {
        _events.Dispose();
    }
}
```

> Event-only の場合、`SendEvent` のみが生成される。`ICommandHandler`、`Handle()`、Binder は生成されない。

## Routing

protobuf メッセージが階層構造を持つ場合（例: `AppAction` が oneof で `PlayerAction` を包むケース）、`[ProtoHandler]` に inner 型を指定するだけで、Source Generator が root 型までのルーティング経路をコンパイル時に自動発見する。

### proto 定義例

```protobuf
// root メッセージ: アプリ全体のアクション
message AppAction {
  oneof action {
    LoadScene load_scene = 1;
    PlayerAction player_action = 2;  // inner 型を包含
  }
  message LoadScene { string scene_name = 1; }
}

// inner メッセージ: プレイヤー固有のアクション
message PlayerAction {
  oneof action {
    Attack attack = 1;
    Defend defend = 2;
  }
  message Attack { int32 damage = 1; }
  message Defend { int32 shield = 1; }
}

// root/inner イベントも同様
message AppState {
  oneof state {
    SceneLoaded scene_loaded = 1;
    PlayerState player_state = 2;
  }
  message SceneLoaded { string scene_name = 1; }
}

message PlayerState {
  oneof state {
    HealthChanged health_changed = 1;
  }
  message HealthChanged { int32 hp = 1; }
}
```

### Handler の実装

```csharp
// inner 型のみ指定 — root 型 (AppAction/AppState) は自動発見される
[ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
public sealed partial class PlayerHandler
{
    readonly Subject<PlayerState> _events = new();

    void HandleAttack(PlayerAction.Types.Attack cmd)
    {
        _events.OnNext(new PlayerState { HealthChanged = new() { Hp = 90 } });
    }

    void HandleDefend(PlayerAction.Types.Defend cmd) { }

    private partial void OnInitialize()
    {
        _events.DistinctUntilChanged().Subscribe(evt => SendEvent(evt));
    }

    private partial void OnDispose()
    {
        _events.Dispose();
    }
}
```

<details>
<summary>Source Generator が生成するルーティングコード</summary>

Gateway との通信は root 型（`AppAction` / `AppState`）で行われる。コマンド側は `BindRouted` で unwrap し、イベント側は `SendEvent` 内にインラインで wrap される:

```csharp
// InjectBindings 内（コマンド受信）:
_binder = MessageBinding.BindRouted<AppAction, PlayerAction>(
    this, gateway,
    root => root.ActionCase == AppAction.ActionOneofCase.PlayerAction ? root.PlayerAction : null);

// SendEvent（イベント送信）:
void SendEvent(PlayerState evt) => _gateway.Send(Any.Pack(new AppState { PlayerState = evt }));
```

- **unwrap（コマンド）**: `BindRouted` で `AppAction` → oneof case チェック → `PlayerAction` を取り出す
- **wrap（イベント）**: `SendEvent` 内で `PlayerState` → `new AppState { PlayerState = inner }` → `Any.Pack()` → `Gateway.Send()`
- root 型を直接指定した場合はルーティングなし（従来の `MessageBinding.Bind` を使用）

</details>

> ルーティングは多段階ネスト（root → mid → inner）にも対応している。

### `[ProtoRoute]` によるルーティング曖昧性の解消

inner 型が複数の親 oneof メッセージに含まれる場合、Source Generator はルーティング経路を一意に決定できず PROTO005 エラーを報告する。`[ProtoRoute]` で経由する親型を指定することで解消できる:

```protobuf
message Info {
  message FCommand { oneof command { DoStuff do_stuff = 1; } message DoStuff {} }
}
message A { oneof action { Info info = 1; } }
message B { oneof action { Info info = 1; } }
```

```csharp
// PROTO005: Info.Types.FCommand は A と B の両方に含まれる
[ProtoHandler(typeof(Info.Types.FCommand))]
public sealed partial class InfoHandler { ... }

// [ProtoRoute] で A 経由を指定 → 解消
[ProtoHandler(typeof(Info.Types.FCommand))]
[ProtoRoute(typeof(A))]
public sealed partial class InfoHandler { ... }
```

多段ネストで中間層が曖昧な場合も、曖昧な階層の型を指定すればよい:

```csharp
// Root → Mid1 → Leaf, Root → Mid2 → Leaf の場合
[ProtoHandler(typeof(Leaf.Types.FCommand))]
[ProtoRoute(typeof(Mid1))]
public sealed partial class LeafHandler { ... }
```

## Architecture

```
External ←─ protobuf bytes ─→ IMessageGateway ←─ Any ─→ Binder<T> ←→ Handler
(Flutter, Native, etc.)                                                  ↑
                                                                [ProtoHandler] で自動生成
                                                User Observable → [R3 operators] → SendEvent()
```

| コンポーネント | 責務 |
|---------------|------|
| `IMessageGateway` | protobuf `Any` の送受信チャネル（利用側で実装） |
| `Binder<T>` | Gateway → Handler の接続・型フィルタリング |
| `ICommandHandler<T>` | 外部 → Unity のコマンド処理 |
| `SendEvent(T)` | Unity → 外部 のイベント送信ヘルパー（自動生成） |
| `[ProtoHandler]` | Source Generator が Handler + SendEvent + Register を自動生成 |

**コマンドフロー (外部 → Unity):**
`Gateway.Messages` → `Where(Any.Is<T>)` → `Unpack<T>()` → `Handler.Handle()`

**イベントフロー (Unity → 外部):**
`User Observable` → `[R3 operators]` → `SendEvent()` → `Any.Pack()` → `Gateway.Send()`

**ルーティング付きフロー（inner 型使用時）:**

```
External ←─ protobuf bytes ─→ Gateway ←─ Any ─→ BindRouted ←→ Handler
                                                    │
                                    Unpack<RootCmd>  │
                                    unwrap(inner)    │
                                                    ↓
                                              inner 型で処理

User Observable → [R3 operators] → SendEvent() → wrap(inner) → Any.Pack() → Gateway.Send()
```

- **コマンド**: `Unpack<AppAction>()` → `unwrap(root) → PlayerAction` → `Handler.Handle()`
- **イベント**: `User Observable` → `SendEvent()` → `wrap(inner) → AppState` → `Any.Pack()` → `Gateway.Send()`

## Source Generator Diagnostics

| ID | Severity | 説明 |
|----|----------|------|
| PROTO001 | Error | oneof case に対応する `Handle` メソッドがない |
| PROTO002 | Error | `Handle` prefix のメソッドがあるが、引数の型がどの oneof case にもマッチしない（typo の可能性） |
| PROTO003 | Error | Event-only クラスに `Handle` prefix のメソッドがある（command type が未指定） |
| PROTO004 | Error | `[ProtoHandler]` が非 `partial` クラスに付与されている |
| PROTO005 | Error | inner 型が複数の親 oneof メッセージに含まれており、ルーティング経路が一意に決定できない。`[ProtoRoute(typeof(...))]` で解消する |
| PROTO006 | Error | `[ProtoRoute]` に指定した型がルーティング経路上の親として見つからない |

> ハンドラメソッドの規約: メソッド名が `Handle` で始まり、引数が oneof case の型と一致する非 static メソッドのみがハンドラとして認識される。`Handle` prefix のないメソッドは無視される。

## Development

### テスト

```bash
# Unity EditMode テスト (43 tests) — Unity Editor を閉じてから実行
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode

# Source Generator テスト (66 tests)
dotnet test ProtoHandlerGenerator.Tests
```

### Source Generator DLL のリビルド

```bash
dotnet build ProtoHandlerGenerator -c Release
cp ProtoHandlerGenerator/bin/Release/netstandard2.0/ProtoHandlerGenerator.dll \
   Packages/com.cortis.core/Editor/ProtoHandlerGenerator/
```

## License

MIT
