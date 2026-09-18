// 兼容不同运行环境的挂载方式
window.GameRecommend = window.GameRecommend || {};
if (typeof GameGlobal !== "undefined") {
    GameGlobal.GameRecommend = window.GameRecommend;
}

// 预加载推荐组件
GameRecommend.preload = function () {
    if (typeof wx === 'undefined' || !wx.createPageManager) {
        console.warn('wx.createPageManager 不可用');
        return;
    }

    var pageManager = wx.createPageManager();
    // 挂载到全局，方便后续使用
    window.__recommendPageManager = pageManager;

    pageManager.load({
        openlink: 'TWFRCqV5WeM2AkMXhKwJ03MhfPOieJfAsvXKUbWvQFQtLyyA5etMPabBehga950uzfZcH3Vi3QeEh41xRGEVFw'
    }).then(function (res) {
        console.log('推荐组件预加载成功', res);
    }).catch(function (err) {
        // 这里用 console.error 打印具体错误对象，避免被误解为函数调用
        console.error('推荐组件预加载失败：', err);
    });
};

// 展示推荐组件
GameRecommend.show = function () {
    var pageManager = window.__recommendPageManager;
    if (!pageManager) {
        console.warn('推荐组件未预加载，请先调用 PreloadRecommend');
        return;
    }

    pageManager.show().then(function () {
        console.log('推荐组件展示成功');
    }).catch(function (err) {
        console.error('推荐组件展示失败：', err);
    });
};