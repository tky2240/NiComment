using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using NiComment.Properties;
using System.Windows;

namespace NiComment
{
    internal class SharedModel
    {
        private ClientWebSocket ws = null;
        public SharedModel()
        {

        }
        public async void ConnectWebSocket(IProgress<Comment> commentProgress)
        {
            ws = new ClientWebSocket();

            Settings settings = Settings.Default;
            //接続先エンドポイントを指定
            var uri = new Uri($"ws://{settings.WebSocketHost}:{settings.WebSocketPort}{settings.WebSocketPath}?ID=master");

            //サーバに対し、接続を開始
            try {
                await ws.ConnectAsync(uri, CancellationToken.None);
            
            
                var buffer = new byte[1024];

                //情報取得待ちループ
                while (true)
                {
                    //所得情報確保用の配列を準備
                    var segment = new ArraySegment<byte>(buffer);

                    //サーバからのレスポンス情報を取得
                        var result = await ws.ReceiveAsync(segment, CancellationToken.None);

                    //エンドポイントCloseの場合、処理を中断
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK",
                          CancellationToken.None);
                        return;
                    }

                    //バイナリの場合は、当処理では扱えないため、処理を中断
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                          "I don't do binary", CancellationToken.None);
                        return;
                    }

                    //メッセージの最後まで取得
                    int count = result.Count;
                    while (!result.EndOfMessage)
                    {
                        if (count >= buffer.Length)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData,
                              "That's too long", CancellationToken.None);
                            return;
                        }
                        segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                        result = await ws.ReceiveAsync(segment, CancellationToken.None);

                        count += result.Count;
                    }

                    //メッセージを取得
                    Comment comment = JsonSerializer.Deserialize<Comment>(Encoding.UTF8.GetString(buffer, 0, count));
                    commentProgress.Report(comment);
                }
            } catch (Exception exception) {
                MessageBox.Show("WebSocket通信に失敗しました");
                MessageBox.Show(exception.StackTrace, exception.Message);
                return;
            } finally {
                ws.Dispose();
            }
        }
        public async void SendMessage(Comment comment) {
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(comment));
            var segment = new ArraySegment<byte>(buffer);

            //クライアント側に文字列を送信
            ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}   
