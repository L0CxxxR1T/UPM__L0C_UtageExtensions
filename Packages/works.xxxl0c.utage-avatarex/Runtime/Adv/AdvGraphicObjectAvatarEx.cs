using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// FileType=AvatarEx 用のグラフィックオブジェクト
    /// AvatarImage は無改造のまま使い、同じ GameObject に AvatarExPostProcessor を追加して
    /// パーツ単位のブレンドモード・クリッピングマスクを後処理で適用する
    /// </summary>
    public class AdvGraphicObjectAvatarEx : AdvGraphicObjectAvatar
    {
        protected override void AddGraphicComponentOnInit()
        {
            base.AddGraphicComponentOnInit();

            // ParentObject.EnableRenderTexture は Init 時点ではまだ LastResource が未設定で
            // 常に false になるため使えない。GameObject の親階層で判定する
            // （RenderTexture 有効時は AdvGraphicObject.InitRenderTextureImage が
            // 　RenderTextureSpace.RenderRoot 配下に TargetObject を生成してから Init するため、
            // 　この時点で親を辿れば判定できる）
            bool isRenderTextureSpace = GetComponentInParent<AdvRenderTextureSpace>(true) != null;

            AvatarExPostProcessor postProcessor = gameObject.AddComponent<AvatarExPostProcessor>();
            postProcessor.Initialize(Avatar, isRenderTextureSpace);
        }
    }
}
