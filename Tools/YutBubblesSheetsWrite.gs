/**
 * 윷 말풍선 JSON → 시트 쓰기용 Apps Script.
 *
 * 설치 (한 번) — 반드시 해당 스프레드시트에서:
 * 1) https://docs.google.com/spreadsheets/d/1d3c7nN8cZjKQUBBetL5B7q6U2hwUBRvwWtL5ys4nrRs
 *    → 확장 프로그램 → Apps Script  (다른 프로젝트에 붙이면 안 됨)
 * 2) 기존 코드 지우고 이 파일 전체를 붙여넣기 → 저장(💾)
 * 3) 배포 → 새 배포 → 유형: 웹 앱
 *    - 설명: yut bubbles write
 *    - 실행 계정: 나
 *    - 액세스 권한: 모든 사용자  ← "나만"/"Google 계정 사용자"면 실패
 * 4) 권한 승인(필요 시) 후 웹 앱 URL(.../exec) 복사
 * 5) 브라우저에서 그 URL을 연다 → {"ok":true,"service":"yut-bubbles-write"} 가 보여야 정상
 * 6) Tools/yut_bubbles_sheets.config.json 의 write_url 에 그 URL 저장
 * 7) 코드를 고친 뒤에는 배포 관리 → 연필 → 버전: 새 버전 → 배포
 *
 * 로컬: npm run yut-bubbles:push
 */

var SPREADSHEET_ID = '1d3c7nN8cZjKQUBBetL5B7q6U2hwUBRvwWtL5ys4nrRs';

function onOpen() {
  SpreadsheetApp.getUi()
    .createMenu('Yut Bubbles')
    .addItem('쓰기 엔드포인트 안내', 'showWriteHelp')
    .addToUi();
}

function showWriteHelp() {
  SpreadsheetApp.getUi().alert(
    '브라우저에서 write_url 을 열었을 때\n' +
      '{"ok":true,"service":"yut-bubbles-write"} 가 보여야 합니다.\n' +
      'HTML/404가 나오면 웹 앱을 새 버전으로 다시 배포하세요.'
  );
}

/** 브라우저 확인용 — 이게 JSON이면 배포 성공 */
function doGet() {
  return jsonOut_({ ok: true, service: 'yut-bubbles-write' });
}

/**
 * POST body JSON:
 * {
 *   "token": "...",
 *   "tab": "yut_bubbles",
 *   "headers": ["id","note","text_ko",...],
 *   "rows": [["rabbit.yut","...", "..."], ...]
 * }
 */
function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) {
      return jsonOut_({ ok: false, error: 'empty body' });
    }
    const body = JSON.parse(e.postData.contents);
    const expected = PropertiesService.getScriptProperties().getProperty('WRITE_TOKEN');
    if (expected && body.token !== expected) {
      return jsonOut_({ ok: false, error: 'unauthorized' });
    }

    const tab = (body.tab || 'yut_bubbles').toString();
    const headers = body.headers;
    const rows = body.rows;
    if (!Array.isArray(headers) || headers.length === 0) {
      return jsonOut_({ ok: false, error: 'headers required' });
    }
    if (!Array.isArray(rows)) {
      return jsonOut_({ ok: false, error: 'rows required' });
    }

    const spreadsheetId = (body.spreadsheetId || SPREADSHEET_ID).toString();
    const ss = SpreadsheetApp.openById(spreadsheetId);
    let sheet = ss.getSheetByName(tab);
    if (!sheet) {
      sheet = ss.insertSheet(tab);
    }

    sheet.clearContents();
    const values = [headers].concat(rows.map(function (r) {
      const out = [];
      for (var i = 0; i < headers.length; i++) {
        out.push(r[i] == null ? '' : String(r[i]));
      }
      return out;
    }));
    sheet.getRange(1, 1, values.length, headers.length).setValues(values);

    return jsonOut_({
      ok: true,
      spreadsheetId: ss.getId(),
      spreadsheetName: ss.getName(),
      tab: sheet.getName(),
      sheetId: sheet.getSheetId(),
      rows: rows.length,
      columns: headers.length,
      sheetNames: ss.getSheets().map(function (s) { return s.getName(); }),
    });
  } catch (err) {
    return jsonOut_({ ok: false, error: String(err) });
  }
}

function jsonOut_(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
