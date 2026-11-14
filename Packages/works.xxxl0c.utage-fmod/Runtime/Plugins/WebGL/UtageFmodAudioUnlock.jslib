// FMODのHTML5出力は window の click / touchend でしか AudioContext を開始しないため、
// キー入力などでも開始できるように同じ処理をキャプチャフェーズから呼び出す。
// Module のプロパティ名はFMOD本体(libfmodstudio.a)の実装に合わせている。
mergeInto(LibraryManager.library, {
  UtageFmod_RegisterAudioUnlock: function () {
    if (Module["UtageFmod_audioUnlockRegistered"]) {
      return;
    }
    Module["UtageFmod_audioUnlockRegistered"] = true;

    var win = Module.window ? Module.window : window;
    var handlerNames = ["OutputAudioWorklet_resumeAudio", "OutputWebAudio_resumeAudio"];
    var eventNames = ["keydown", "mousedown", "pointerup", "touchend", "click"];

    function onUserInput() {
      if (Module.mInputRegistered) {
        // 初回はFMODのハンドラに任せる。出力ノードの接続やWorkletの読み込みもここで行われる
        for (var i = 0; i < handlerNames.length; i++) {
          var handler = Module[handlerNames[i]];
          if (typeof handler === "function") {
            handler();
          }
        }
        return;
      }

      // ユーザー操作として扱われない入力で開始に失敗した場合や、
      // ブラウザ側で再び停止された場合に備えて、次の入力で再開を試みる
      var context = Module.mContext || Module.context;
      if (context && context.state !== "running" && context.state !== "closed") {
        context.resume().catch(function (err) {
          console.warn("[UtageFmod] AudioContextの再開に失敗しました。", err);
        });
      }
    }

    for (var j = 0; j < eventNames.length; j++) {
      win.addEventListener(eventNames[j], onUserInput, { capture: true, passive: true });
    }
  },
});
