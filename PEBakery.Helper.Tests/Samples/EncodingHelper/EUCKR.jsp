




<!DOCTYPE html PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">
<html>
<head>
	<meta http-equiv="X-UA-Compatible" content="IE=edge">
	<meta http-equiv="Content-Language" content="ko">
	<meta http-equiv="imagetoolbar" content="no">
	<meta http-equiv="Content-Type" content="text/html; charset=euc-kr">
	<title>고려대 수강신청</title>
	<link href="/style/sugang.css" rel="stylesheet" type="text/css">
	<style type="text/css">
	* { font-family:AppleGothic, NanumGothic, 나눔고딕, "Malgun Gothic", "맑은 고딕", Dotum, 돋움, Tahoma, Sans-serif; font-size:12px; }
	img { border:0; }
	#top { height:52px; background: url("images/tit_bg.gif"); padding:15px 0 0 20px; }
	
	.input_login { border:#ccccc5 1px solid; background-color:#e1e1d4; padding-left:3px; height:15px; width:130px; }
	
	#loginBox { margin-top:10px; margin-left:20%; }
	#loginBox fieldset { 
		width:440px; 
		border:5px solid #e6e3d5;
		background:#f2f2ea;
		padding:10px 0px 10px 0px;
	}
	#loginBox dl { float:left; overflow:hidden; width:280px; text-align:right; }
	#loginBox dt { float:left; width:140px; margin-top:5px; }
	#loginBox dd { margin-bottom: 5px; }
	#loginButton { float:left; margin-left:5px; }
	#loginButton ul{ list-style:none; padding:0px; }

	#sugangGuid { margin-left:10%; margin-top:20px; }
	#sugangGuid ul {
		margin: 0;
		padding: 0;
		list-style: none;
	}
	#sugangGuid li {
		display:block;
		padding:5px 0 5px 15px;
		background: url(images/bullet_logtxt.gif) no-repeat left center;
		text-align:left;
	}
	
	#sugangGuid li.underline{
		text-decoration: underline;
	}
	
	#sugangGuid li.innerline{
		margin-left:53px;
		background: url(images/new/bul/bul_01.png) no-repeat left center; 
	}
	
	.tit_redbullet{display:block;min-height:16px;padding-left:20px;margin:25px 0 0 0;font:bold 12px/17px Dotum;color:#000;background:url(/images/new/bul/bul_32.png) no-repeat left top}
	.inner_cont{padding-left: 35px;}
	</style>

<script type="text/javascript" src="/js/netfunnel.js" charset="utf-8"></script>
<script type="text/javascript">
	function login(obj){
	   strID = loginForm.id.value;
	   if(strID.length == 0 || strID == " "){
	      	alert("사용자 ID가 입력되지 않았습니다.");
	    	document.getElementById('id').focus();      
		  	return;
	   }
	   strPW = loginForm.pw.value;
	   if(strPW.length == 0 || strPW == " "){
			alert("암호가 입력되지 않았습니다.");
			return;
	   } else {
			loginForm.action = "https://sugang.korea.ac.kr/LoginAction.jsp?menuDiv=1";
			//loginForm.submit();
			//netfunnel_comment [1_2_1] login 
			NetFunnel_Action({action_id: "login",popup_target:top.firstF, use_frame_block:true, frame_block_list:[{win:top.secondF}]},loginForm);
		}
	}

	function f_open3(){
		window.open("./lecture/time_table.htm","new_notice","width=850,height=700,top=0,left=100,toolbar=no,status=no,menubar=no,scrollbars=yes,resizable=yes");
	}

	function  openNewWindow(){
	   var url = '/course/MedicalExamInfo.htm';
	   var imywidth;
	   var imyheight;
	   imywidth = (window.screen.width/2) - (247+10);
	   imyheight = (window.screen.height/2) - (185+50);
	   var newwin = window.open(url,"_blank", "scrollbars=no ,resizable=no,copyhistory=no,width=550, height=400" + ",left=" + imywidth + ",top=" + imyheight);
	}

	function  openNotice(){
		alert("금일 오전 10시부터 10시 45분까지 인원제한과목을 수강신청하실 수 없었습니다. \n\n10시 45분 경부터 오류가 수정되어 인원제한과목신청이 가능해졌습니다.\n\n사용에 불편을 드려서 대단히 죄송합니다.");
	}

	function f_calls(str) {
		switch (str) {
			case 0 :
				window.open("http://sugang.korea.ac.kr/elective_info0001.htm", "elective information", "left=0, top=0, width=620, height=530,resizable=no,status=no,toolbar=no,menubar=no,scrollbars=no");
				break;
			case 1 :
				window.open("https://infodepot.korea.ac.kr/student/exchangestudent/psearch.jsp", "exchangestudent", "left=0, top=0, width=620, height=530,resizable=no,status=no,toolbar=no,menubar=no,scrollbars=no");
				break;
			default:;
		}
	}
	
	function f_onload(){
		document.getElementById('id').focus();
	}
</script>
</head>
<body onload="f_onload();" >
<div id="wrap">
<div id="top" align="left">

	<img src="images/tit_app_sug.gif" alt="학부수강신청시스템" width="170" height="21">

</div>



	<div id="loginBox">
		<form method="post" name="loginForm">
		<fieldset>
			<dl>
				<dt><label for="id"><img src="images/login_num.gif" alt="학번" width="45" height="12"></label></dt>
				<dd><input type="text" id="id" name="id" value="" class="input_login"></dd>
				<dt><label for="pw"><img src="images/login_pw.gif" alt="암호" width="45" height="12"></label></dt>
				<dd><input type="password" id="pw" name="pw" value="" class="input_login" onkeydown="if(event.keyCode==13) login();"></dd>
			</dl>
			<div id="loginButton">
				<ul>
					<li><a href="#" onclick="login()"><img src="images/btn_login.gif" alt="login" border="0"></a></li>
				</ul>
			</div>
		</fieldset>
		</form>
	</div>

	<div id="sugangGuid">
		<!-- 수강신청 안내사항 -->
		<span class="tit_redbullet">수강신청 시스템 사용안내</span>
		<div class="inner_cont">
			<ul>
				<li><a target="_blank" href="./notice_macro.htm"><span style="font-weight:bold; color:red; text-decoration:underline;">수강신청시스템 중복로그인/매크로 제한 기능 도입 안내</span></a></li>
				<li><a target="_blank" href="./guide.htm"><span style="font-weight:bold; color:red; text-decoration:underline;">로그인이 되지 않을 때, Internet Explorer 설정사항 안내</span></a></li>
				<li><span class="font_red" style="color:red;">수강희망과목 등록기간 안내 -    2. 4(화) 10:00 - 2. 7(금) 12:00</span></li>
				<li>학사관련 주요사항 안내는 교육정보 홈페이지를 참조하세요. <a target="_blank" href="http://registrar.korea.ac.kr"><span style="font-weight:bold; color: red;text-decoration:underline;">교육정보 바로가기</span></a></li>
				<li>화면왼쪽 메뉴에 있는 수강신청안내, 단대별 수강신청유의사항을 숙지하신 후 수강신청을 하십시오.</li>
				<li><span class="bold">암호</span> - 포털(KUPID)사용자 : 포털비밀번호</li>
				<li class="innerline">포털(KUPID)미사용자 : '포털미사용자 비밀번호변경'에서 설정한 비밀번호(설정전: 주민번호뒷자리)</li>
				<li class="innerline">(포털사용중인 신입생도 개강전에는 포털미사용자에 해당하는 비밀번호 사용)</li>				
				<li>암호 분실시 - 포털 사용자 : 포털(http://portal.korea.ac.kr) 로그인 화면의 '비밀번호찾기'에서 비밀번호 재발급</li>
				<li class="innerline">포털(KUPID)미사용자 : 수강신청(http://sugang.korea.ac.kr) '포털미사용자 비밀번호변경' 메뉴에서 비밀번호 재발급</li>
				<li class="innerline"><span class="font_red" style="font-weight:bold;">포털에서 비밀번호를 변경 또는 재발급 받은 경우는 10분후에 로그인 하기 바랍니다.</span></li>
				<li class="underline"><a href="#" onClick="f_calls(1)">국내 교류 학생의 학번 확인</a></li>
				<li>Internet Explorer 9.0 이상의 버전, 화면 해상도 1024*768에 최적화 되어 있습니다.</li>
			</ul>
		</div>		
	</div>
</div>
<br/>
<br/>
</body>
</html>
