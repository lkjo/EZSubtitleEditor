#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Whisper 語音辨識腳本
使用 openai-whisper 函式庫進行語音轉文字辨識
"""

import whisper
import argparse
import json
import sys


def main():
    """主要執行函數"""
    # 步驟 2：設定命令列參數解析
    parser = argparse.ArgumentParser(
        description='使用 Whisper 模型進行語音辨識',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
使用範例:
  python transcribe.py --audio_path audio.wav --model base --language zh
  python transcribe.py --audio_path audio.mp3 --model small --language en
        """
    )
    
    # 定義必要的命令列參數
    parser.add_argument(
        '--audio_path',
        required=True,
        help='音訊檔案的路徑'
    )
    
    parser.add_argument(
        '--model',
        required=True,
        choices=['tiny', 'base', 'small', 'medium', 'large', 'large-v2', 'large-v3'],
        help='要使用的 Whisper 模型名稱 (tiny, base, small, medium, large, large-v2, large-v3)'
    )
    
    parser.add_argument(
        '--language',
        required=True,
        help='要辨識的語言代碼 (例如: en, zh, ja, ko, fr, de, es, ru, it, pt, nl, ar, tr, pl, ca, uk, sv, cs, hr, et, fi, hu, lt, lv, mt, sk, sl, bg, ro, el, he, fa, ur, hi, th, vi, my, km, lo, si, ne, bn, ml, ta, te, kn, gu, pa, or, as, mr, sa, cy, br, oc, ms, su, jw, mg, lb, is, mk, be, kk, ky, uz, tg, mn, hy, ka, am, ti, om, so, sw, rw, yo, lg, ln, ny, sn, zu, af, sq, eu, gl, mt, ga, gd, cy, br, oc, co, la, eo, ia, ie, vo, an, nv, cr, iu, oj, cw, ch, st, tn, ts, ve, xh, nr, ss, nso, pedi, tso, ven)'
    )
    
    # 解析命令列參數
    args = parser.parse_args()
    
    try:
        # 步驟 3：主邏輯實作
        
        # 載入指定的 Whisper 模型
        print(f"正在載入 Whisper 模型: {args.model}...", file=sys.stderr)
        model = whisper.load_model(args.model)
        
        # 執行語音辨識
        print(f"正在辨識音訊檔案: {args.audio_path}...", file=sys.stderr)
        print(f"辨識語言: {args.language}", file=sys.stderr)
        
        result = model.transcribe(
            args.audio_path, 
            language=args.language, 
            verbose=False
        )
        
        # 從 result["segments"] 中提取需要的資訊
        segments_data = []
        
        for segment in result["segments"]:
            segment_info = {
                "start": segment["start"],
                "end": segment["end"],
                "text": segment["text"].strip()
            }
            segments_data.append(segment_info)
        
        # 轉換為 JSON 格式並輸出到 stdout
        json_output = json.dumps(
            segments_data, 
            ensure_ascii=False,  # 確保中文字元能被正確處理
            indent=None,         # 緊湊格式
            separators=(',', ':')  # 移除多餘空格
        )
        
        # 將 JSON 字串輸出到標準輸出
        print(json_output)
        
        # 輸出統計資訊到 stderr（不會影響 stdout 的 JSON 輸出）
        print(f"辨識完成！共產生 {len(segments_data)} 個文字區段。", file=sys.stderr)
        
    except FileNotFoundError:
        print(f"錯誤：找不到音訊檔案 '{args.audio_path}'", file=sys.stderr)
        sys.exit(1)
        
    except Exception as e:
        print(f"辨識過程中發生錯誤：{str(e)}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main() 