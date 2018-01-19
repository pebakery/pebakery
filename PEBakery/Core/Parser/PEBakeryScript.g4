/*
    Copyright (C) 2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/


grammar PEBakeryScript;

// disable CS3021 warning 
// https://github.com/tunnelvisionlabs/antlr4cs/issues/133
// https://github.com/antlr/grammars-v4/tree/master/csharp
// https://github.com/antlr/antlr4/issues/862
/*
 * Parser Rules
 */

codes : block EOF;

block : stmt*;

stmt : ifStmt | normalStmt;

ifStmt : IF P_ ifCond P_ branchBlock elseStmt?;

elseStmt : ELSE P_ branchBlock;

// normalStmt : STRLINE NEWLINE;
normalStmt : STR? (P_ STR)* NEWLINE;

branchBlock : (BEGIN NEWLINE block END NEWLINE) | stmt;

ifCond : (
    ifCondComp | ifCondArg1 | ifCondArg2 | ifCondArg3
);

ifCondComp : (
    (NOT P_)?
    STR P_
    (
        EQUAL | SMALLER | SMALLEREQUAL | BIGGER | BIGGEREQUAL | EQUALX |
        NOTEQUAL // Will-be-deprecated
    ) P_
    STR
);

ifCondArg1 : (
    (NOT P_)?
    (
        EXISTFILE | EXISTDIR | EXISTVAR | EXISTMACRO | PING | QUESTION |
        NOTEXISTFILE | NOTEXISTDIR | NOTEXISTVAR // Will-be-deprecated
    ) P_
    STR
);

ifCondArg2 : (
    (NOT P_)?
    (
        EXISTSECTION | EXISTREGSECTION | EXISTREGSUBKEY | 
        NOTEXISTSECTION | NOTEXISTREGSECTION // Will-be-deprecated
    ) P_
    STR P_
    STR
);

ifCondArg3 : (
    (NOT P_)?
    (
        EXISTREGKEY | EXISTREGVALUE | QUESTION |
        NOTEXISTREGKEY // Will-be-deprecated
    ) P_
    STR P_
    STR P_
    STR
);

fragment A : ('a'|'A');
fragment B : ('b'|'B');
fragment C : ('c'|'C');
fragment D : ('d'|'D');
fragment E : ('e'|'E');
fragment F : ('f'|'F');
fragment G : ('g'|'G');
fragment H : ('h'|'H');
fragment I : ('i'|'I');
fragment J : ('j'|'J');
fragment K : ('k'|'K');
fragment L : ('l'|'L');
fragment M : ('m'|'M');
fragment N : ('n'|'N');
fragment O : ('o'|'O');
fragment P : ('p'|'P');
fragment Q : ('q'|'Q');
fragment R : ('r'|'R');
fragment S : ('s'|'S');
fragment T : ('t'|'T');
fragment U : ('u'|'U');
fragment V : ('v'|'V');
fragment W : ('w'|'W');
fragment X : ('x'|'X');
fragment Y : ('y'|'Y');
fragment Z : ('z'|'Z');

IF : I F;
ELSE : E L S E;
BEGIN : B E G I N;
END : E N D;
EXISTFILE : E X I S T F I L E;
EXISTDIR : E X I S T D I R;
EXISTSECTION : E X I S T S E C T I O N;
EXISTREGSECTION : E X I S T R E G S E C T I O N;
EXISTREGSUBKEY : E X I S T R E G S U B K E Y;
EXISTREGKEY : E X I S T R E G K E Y;
EXISTREGVALUE : E X I S  T R E G V A L U E;
EXISTVAR : E X I S T V A R;
EQUAL : ((E Q U A L) | '==');
NOTEQUAL : ((N O T E Q U A L) | '!=');
SMALLER : ((S M A L L E R) | '<');
SMALLEREQUAL : ((S M A L L E R E Q U A L) | '<=');
BIGGER : ((B I G G E R) | '>');
BIGGEREQUAL : ((B I G G E R E Q U A L) | '>=');
EQUALX : (E Q U A L X | '===');
PING : P I N G;
ONLINE : O N L I N E;
NOT : N O T;
QUESTION : Q U E S T I O N;
EXISTMACRO : E X I S T M A C R O;
NOTEXISTFILE : N O T E X I S T F I L E; // Compatbility
NOTEXISTDIR : N O T E X I S T D I R; // Compatbility
NOTEXISTSECTION : N O T E X I S T S E C T I O N; // Compatbility
NOTEXISTREGSECTION : N O T E X I S T R E G S E C T I O N; // Compatbility
NOTEXISTREGKEY : N O T E X I S T R E G K E Y; // Compatbility
NOTEXISTVAR : N O T E X I S T V A R; // Compatbility

// Variable Name %a%, #1
// VARNAME : ('%' [a-zA-Z0-9_\-]+ '%' | '#' [0-9]+);
// COMMENT : ('//'|';'|'#') ~(',')+;
NEWLINE : ('\r\n')|('\n');
P_ : ','; // PERIOD

// STRLINE : (~[\r\n])+?;
STR : (' '*? (~[,\r\n])+ ' '*?) | (' '*? '"' ()* '"' ' '*?);

/*
cmd : (
// 01 CommandFile
    cmd_filecopy | cmd_filedelete | cmd_filemove | cmd_filecreateblank | cmd_filesize | cmd_fileversion |
    cmd_dircopy | cmd_dirdelete | cmd_dirmove | cmd_dirmake | cmd_dirsize |
// 02 CommandRegistry
    cmd_reghiveload | cmd_reghiveunload | 
    cmd_regimport | cmd_regexport |
    cmd_regread | cmd_regwrite | cmd_regdelete | cmd_multi |
// 03 CommandFile
    cmd_txtaddline | cmd_txtreplace | cmd_txtdelline | cmd_txtdelspaces | cmd_txtdelemptylines |
// 04 CommandINI
    cmd_iniread | cmd_iniwrite | cmd_inidelete | cmd_iniaddsection | cmd_inideletesection | cmd_iniwritetextline | cmd_inimerge | 
// 05 CommandArchive
    cmd_compress | cmd_decompress | cmd_expand | cmd_copyorexpand |
// 06 CommandNetwork
    cmd_webget |
// 07 CommandPlugin
    cmd_extractfile | cmd_extractandrun | cmd_extractallfiles | cmd_encode | 
// 08 CommandInterface
    cmd_visible | cmd_message | cmd_echo | cmd_addinterface | cmd_userinput | 
    // cmds_interface | cmds_hash | cmds_STR | cmds_math | cmds_system | cmds_branch | cmds_control
) NEWLINE;

// 01 CommandFile
cmd_filecopy : FILECOPY P_ STR P_ STR cmd_filecopy_options?;
cmd_filecopy_options : (P_ (PRESERVE | NOWARN | NOREC))+;
cmd_filedelete : FILEDELETE P_ STR cmd_filedelete_options?;
cmd_filedelete_options : (P_ (NOWARN | NOREC))+;
cmd_filemove : (FILERENAME | FILEMOVE) P_ STR P_ STR;
cmd_filecreateblank : FILECREATEBLANK P_ STR cmd_filecreateblank_options?;
cmd_filecreateblank_options : (P_ (PRESERVE | NOWARN | (UTF8 | UTF16LE | UTF16BE | ANSI)))+; 
cmd_filesize : FILESIZE P_ STR P_ VARNAME;
cmd_fileversion : FILEVERSION P_ STR P_ VARNAME;
cmd_dircopy : DIRCOPY P_ STR P_ STR;
cmd_dirdelete : DIRDELETE P_ STR;
cmd_dirmove : DIRMOVE P_ STR P_ STR;
cmd_dirmake : DIRMAKE P_ STR;
cmd_dirsize : DIRSIZE P_ STR P_ VARNAME;

// 02 CommandRegistry
cmd_reghiveload : REGHIVELOAD P_ STR P_ STR;
cmd_reghiveunload : REGHIVEUNLOAD P_ STR;
cmd_regimport : REGIMPORT P_ STR;
cmd_regexport : REGEXPORT P_ STR P_ STR;
cmd_regread : REGREAD P_ STR P_ STR P_ STR P_ VARNAME;
cmd_regwrite : REGWRITE P_ STR P_ STR P_ STR P_ STR (P_ STR)*;
cmd_regdelete : REGDELETE P_ STR P_ STR (P_ STR)?;
cmd_multi : REGMULTI P_ STR P_ STR P_ STR P_ STR P_ STR (P_ STR)?;

// 03 CommandText
cmd_txtaddline : TXTADDLINE P_ STR P_ STR P_ STR (P_ STR)?;
cmd_txtreplace : TXTREPLACE P_ STR P_ STR P_ STR;
cmd_txtdelline : TXTDELLINE P_ STR P_ STR;
cmd_txtdelspaces : TXTDELSPACES P_ STR;
cmd_txtdelemptylines : TXTDELEMPTYLINES P_ STR;

// 04 CommandINI
cmd_iniread : INIREAD P_ STR P_ STR P_ STR P_ STR;
cmd_iniwrite : INIWRITE P_ STR P_ STR P_ STR P_ STR;
cmd_inidelete : INIDELETE P_ STR P_ STR P_ STR;
cmd_iniaddsection : INIADDSECTION P_ STR P_ STR;
cmd_inideletesection : INIDELETESECTION P_ STR P_ STR;
cmd_iniwritetextline : INIWRITETEXTLINE P_ STR P_ STR P_ STR (P_ APPEND)?;
cmd_inimerge : INIMERGE P_ STR P_ STR;

// 05 CommandArchive
cmd_compress : COMPRESS P_ ARCHIVE_COMPRESS_FORMAT P_ STR P_ STR cmd_compress_options?;
cmd_compress_options : (P_ (ARCHIVE_COMPRESS_LEVEL | (UTF8 | UTF16LE | UTF16BE | ANSI)))+;
cmd_decompress : DECOMPRESS P_ STR P_ STR (P_ (UTF8 | UTF16LE | UTF16BE | ANSI))?;
cmd_expand : EXPAND P_ STR P_ STR cmd_expand_options?;
cmd_expand_options : (P_ (SINGLEFILE | PRESERVE | NOWARN))+;
cmd_copyorexpand : COPYOREXPAND P_ STR P_ STR cmd_copyorexpand_options?;
cmd_copyorexpand_options : (P_ (PRESERVE | NOWARN))+;

// 06 CommandNetwork
cmd_webget : (WEBGET | WEBGETIFNOTEXIST) P_ STR P_ STR (P_ STR P_ STR)?;

// 07 CommandPlugin
cmd_extractfile : EXTRACTFILE P_ STR P_ STR P_ STR P_ STR;
cmd_extractandrun : EXTRACTANDRUN P_ STR P_ STR P_ STR;
cmd_extractallfiles : EXTRACTALLFILES P_ STR P_ STR P_ STR;
cmd_encode : ENCODE P_ STR P_ STR P_ STR;

// 08 CommandInterface
cmd_visible : VISIBLE P_ STR P_ STR;
cmd_message : MESSAGE P_ STR cmd_message_options?;
cmd_message_options : (P_ ((INFORMATION | CONFIRMATION | ERROR | WARNING) | STR))+;
cmd_echo : ECHO P_ STR (P_ WARN)?;
cmd_addinterface : ADDINTERFACE P_ STR P_ STR P_ STR;
cmd_userinput : cmd_userinput_dirpath | cmd_userinput_filepath;
cmd_userinput_dirpath : USERINPUT P_ DIRPATH P_ STR P_ STR;
cmd_userinput_filepath : USERINPUT P_ FILEPATH P_ STR P_ STR;
 */
/*
 * Lexer Rules
 */

// fragments


/*
// cmds
// 01 File - 11
FILECOPY : F I L E C O P Y;
FILEDELETE : F I L E D E L E T E;
FILERENAME : F I L E R E N A M E;
FILEMOVE : F I L E M O V E;
FILECREATEBLANK : F I L E C R E A T E B L A N K;
FILESIZE : F I L E S I Z E;
FILEVERSION : F I L E V E R S I O N;
DIRCOPY : D I R C O P Y;
DIRDELETE : D I R D E L E T E;
DIRMOVE : D I R M O V E;
DIRMAKE : D I R M A K E;
DIRSIZE : D I R S I Z E;

// 02 Registry - 8
REGHIVELOAD : R E G H I V E L O A D;
REGHIVEUNLOAD : R E G H I V E U N L O A D;
REGIMPORT : R E G I M P O R T;
REGEXPORT : R E G E X P O R T;
REGREAD : R E G R E A D;
REGWRITE : R E G W R I T E;
REGDELETE : R E G D E L E T E;
REGMULTI : R E G M U L T I;

// 03 STR - 5
TXTADDLINE : T X T A D D L I N E;
TXTREPLACE : T X T R E P L A C E;
TXTDELLINE : T X T D E L L I N E;
TXTDELSPACES : T X T D E L S P A C E S;
TXTDELEMPTYLINES : T X T D E L E M P T Y L I N E S;

// 04 INI - 7
INIREAD : I N I R E A D;
INIWRITE : I N I W R I T E;
INIDELETE : I N I D E L E T E;
INIADDSECTION : I N I A D D S E C T I O N;
INIDELETESECTION : I N I D E L E T E S E C T I O N;
INIWRITETEXTLINE : I N I W R I T E T E X T L I N E;
INIMERGE : I N I M E R G E;

// 05 Archive - 4
COMPRESS : C O M P R E S S;
DECOMPRESS : D E C O M P R E S S;
EXPAND : E X P A N D;
COPYOREXPAND : C O P Y O R E X P A N D;

// 06 Network - 2
WEBGET : W E B G E T;
WEBGETIFNOTEXIST : W E B G E T I F N O T E X I S T;

// 07 Plugin - 4
EXTRACTFILE : E X T R A C T F I L E;
EXTRACTANDRUN : E X T R A C T A N D R U N;
EXTRACTALLFILES : E X T R A C T A L L F I L E S;
ENCODE : E N C O D E;

// 08 Interface - 5
VISIBLE : V I S I B L E;
MESSAGE : M E S S A G E;
ECHO : E C H O;
USERINPUT : U S E R I N P U T;
ADDINTERFACE : A D D I N T E R F A C E;
RETRIEVE : R E T R I E V E; // Compatbility
DIRPATH : D I R P A T H;
FILEPATH : F I L E P A T H;

// 09 Hash
HASH : H A S H;
MD5 : M D '5';
SHA1 : S H A '1';
SHA256 : S H A '256';
SHA384 : S H A '384';
SHA512 : S H A '512';

// 10 STR
STRFORMAT : S T R F O R M A T;
BYTES : B Y T E S; // Compatbility
INTTOBYTES : I N T T O B Y T E S;
BYTESTOINT : B Y T E S T O I N T;
CEIL : C E I L;
FLOOR : F L O O R;
ROUND : R O U N D;
DATE : D A T E;
FILENAME : F I L E N A M E;
PATH : P A T H;
EXT : E X T;
INC : I N C;
DEC : D E C;
MULT : M U L T;
DIV : D I V;
LEFT : L E F T;
RIGHT : R I G H T;
SUBSTR : S U B S T R;
LEN : L E N;
LTRIM : L T R I M;
RTRIM : R T R I M;
CTRIM : C T R I M;
NTRIM : N T R I M;
POS : P O S;
POSX : P O S X;
REPLACE : R E P L A C E;
REPLACEX : R E P L A C E X;
SHORTPATH : S H O R T P A T H;
LONGPATH : L O N G P A T H;
SPLIT : S P L I T;

// 11 Math
MATH : M A T H;
ADD : A D D;
SUB : S U B;
MUL : M U L;
INTDIV : I N T D I V;
NEG : N E G;
INTSIGN : I N T S I G N;
INTUNSIGN : I N T U N S I G N;
BOOLAND : B O O L A N D;
BOOLOR : B O O L O R;
BOOLXOR : B O O L X O R;
BOOLNOT : B O O L N O T;
BITAND : B I T A N D;
BITOR : B I T O R;
BITXOR : B I T X O R;
BITNOT : B I T N O T;
BITSHIFT : B I T S H I F T;
ABS : A B S;
POW : P O W;

// 12 System
SYSTEM : S Y S T E M;
CURSOR : C U R S O R;
ERROROFF : E R R O R O F F;
FILEREDIRECT : F I L E R E D I R E C T;
GETENV : G E T E N V;
GETFREEDRIVE : G E T F R E E D R I V E;
GETFREESPACE : G E T F R E E S P A C E;
ISADMIN : I S A D M I N;
ONBUILDEXIT : O N B U I L D E X I T;
ONSCRIPTEXIT : O N S C R I P T E X I T;
ONPLUGINEXIT : O N P L U G I N E X I T;
REFRESHINTERFACE : R E F R E S H I N T E R F A C E;
RESCANSCRIPTS : R E S C A N S C R I P T S;
SAVELOG : S A V E L O G;
SHELLEXECUTE : S H E L L E X E C U T E;
SHELLEXECUTEEX : S H E L L E X E C U T E E X;
SHELLEXECUTEDELETE : S H E L L E X E C U T E D E L E T E;

// 13 Branch
RUN : R U N;
EXEC : E X E C;
LOOP : L O O P;
*/


/*
// 14 Control
SET : S E T;
ADDVARIABLES : A D D V A R I A B L E S;
EXIT : E X I T;
HALT : H A L T;
WAIT : W A I T;
BEEP : B E E P;
 */



// Reserved Keyword
/*
PRESERVE : P R E S E R V E;
WARN : W A R N;
NOWARN : N O W A R N;
NOREC : N O R E C;
SINGLEFILE : S I N G L E F I L E;
UTF8 : U T F '8';
UTF16LE : (U T F '16') | (U T F '16' L E);
UTF16BE : U T F '16' B E;
ANSI : A N S I;
APPEND : A P P E N D;
ARCHIVE_COMPRESS_FORMAT : Z I P;
ARCHIVE_COMPRESS_LEVEL : (S T O R E) | (F A S T E S T) | (N O R M A L) | (B E S T);
INFORMATION : I N F O R M A T I O N;
CONFIRMATION : C O N F I R M A T I O N;
ERROR : E R R O R;
WARNING : W A R N I N G;
 */
