/**
 * (선택) Google Sheets에서 JSON 미리보기용.
 *
 * 프로젝트로 넣는 내보내기는 로컬 스크립트를 쓰세요:
 *   python3 Tools/export_dialogue.py --sheet-id YOUR_ID --character okto
 *
 * 브라우저 다운로드는 Unity 프로젝트에 자동 반영되지 않습니다.
 */

function onOpen() {
  SpreadsheetApp.getUi()
    .createMenu('Dialogue')
    .addItem('안내 보기', 'showExportHelp')
    .addToUi();
}

function showExportHelp() {
  SpreadsheetApp.getUi().alert(
    '프로젝트로 내보내기\n\n' +
      '터미널에서:\n' +
      'python3 Tools/export_dialogue.py --sheet-id SHEET_ID --character okto\n\n' +
      '결과는 Assets/Resources/Dialogue/okto_tutorial.json 에 저장됩니다.\n' +
      'Tools/dialogue_sheets.config.json 에 sheet_id를 넣으면\n' +
      'python3 Tools/export_dialogue.py 만으로도 됩니다.'
  );
}
