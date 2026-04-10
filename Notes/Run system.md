
ריצה היא כניסה למבוך עצמו / חדרי הקרב
לוגיקה של חדר
* כל חדר יהיה ברוחב קבוע ואורך משתנה
* כל חדר מכיל אובים ואולי גם מלכודות
* כל חדר שעוברים האוייבים עולים רמות
* בתוך חדר יש 3 ראונדלים כשהשחקן חיסלת את כל האויבים בחדר סבב נוסף נפתח בתוך החדר ונוצרים עוד אויבים כל ראונד נגמר כשהרגת את כל האויבים או כשעבר זמן (בראונד האחרון אין טיימר זה או פסילה או מעבר לשלב הבא)
* שער לחדר הבא נפתח בראש החדר כשכל הראונדים הסתיימו כל האוייבים הובסו ובמידה והשחקן עלה רמה אז עד שהוא ברח שידרוג
* במעבר לחדר הבא כל הסטטים של השחקן נשארים אותו הדבר ללא שינוי.
* בכל סוף שלב השחקן מקבל EXP (בעלית רמה נפתח ידאלוג שיפור)(כלל הניראה כמות הEXP תהיה קבוע עליית הרמה תהיה בפרקי זמן די קבועים)
* בכל 10 שלבים יש בוס חדר בדרך כלל עם ראונד 1
* בשלב 50 הבוס האחרון ואחריו נגמרת הריצה וחוזרים לשלב הראשון אבל ממשיכים באותה רמת קושי שממשיכה ללכת ולגדול כל הזמן.
* כשהשחקן מת ואין לו יותר אפשרות להחיות את עצמו חוזרים לעמוד הבית



Build the core run system for my game.

Run = entering the dungeon and progressing through combat rooms.

Rules:
- Each room has fixed width and variable length.
- Each room contains enemies and optionally traps.
- Enemies scale up in level every room.
- round = spawn batch of enemies and count down timer to the next round
- A round ends when all enemies are defeated or when the timer ends.
- Each room has 3 rounds.
- In the final round there is no timer: only success or failure.
- The gate to the next room opens only after all rounds are completed and  all enemies are defeated, and if the player leveled up, only after choosing an upgrade.
- When moving to the next room, the player keeps all current stats unchanged.
- At the end of each stage, the player gains EXP.
- On level up, open an upgrade selection dialog.
- EXP gain should probably be fixed so level-ups happen at fairly regular intervals.
- Every 10 stages, spawn a boss room, usually with 1 round.
- At stage 50, spawn the final boss.
- After defeating the final boss, the run loops back to stage 1, but difficulty keeps increasing continuously.
- If the player dies and has no revive left, return to the home screen.

Implement a clean, extensible base architecture for this run flow.
Prefer simple, maintainable systems and clear separation of responsibilities.




  

### 2. Difficulty scaling

Build the difficulty scaling system for my game run.  
  
Rules:  
- Every 10 stages, spawn a boss room, usually with 1 round.  
- At stage 50, spawn the final boss.  
- After defeating the final boss, the run loops back to stage 1, but difficulty keeps increasing continuously.  
*  this is co op game take in account using networking architecture
  
Implement a clean, extensible base architecture for this system.  
Prefer simple, maintainable systems and clear separation of responsibilities.


### 5. Death / Revive / Return home

Build the death and revive flow for my game run.  
  
Rules:  
- If the player dies and has no revive left, return to the home screen.  
- this is co op game take in account using networking architecture
  
Implement a clean, extensible base architecture for this system.  
Prefer simple, maintainable systems and clear separation of responsibilities.