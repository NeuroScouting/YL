using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

public class XmlManager {
// Should only have to change these first two
	public static string windowsRoot = @"C:\NeuroScouting\Dropbox";
	public static string macRoot = @"/Users/margaretsheridan/Desktop";
	public static string IOSRoot = Application.persistentDataPath;

	public static string dashboardFolderName = "yl";
	
	//Folder names
	public static string logFilesFolderName = "log_files";
	public static string traceFilesFolderName = "trace_files";
	public static string playerSpecificFolderName = "player_specific";
	public static string sessionFilesName = "session_files";
	public static string playermeasuresName = "indiv_measures";
	public static string pitchFilesName = "pitch_files";
	public static string unfinishedSessionFolderName = "unfinished_files";
	
	//The delimiter used for Path
	public static char delim = Path.DirectorySeparatorChar;
	
	//The game manager class
	private GameManager gm;
	
	//The username
	private string mUserName ="testGuy";
	//The file name of the write out file
	public string statsXML = "";
	//The file name of the read in file
	public string sessionXML = "randomList";

	public static string DropboxPath
	{
		get
		{
			if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor) {
				return windowsRoot;
			} else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor) {
				return macRoot;
			} else if (Application.platform == RuntimePlatform.IPhonePlayer) {
				return IOSRoot;
			} else {
				//In the default case, just return whatever persistent data path the platform has.
				return Application.persistentDataPath;
			}
		}
	}

	//Methods that return the path to specific folder names
	public static string DashboardPath{
		get { return DropboxPath + delim + dashboardFolderName; }
	}

	public static string LogFilesPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + logFilesFolderName; }
	}

	public static string TraceFilesPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + traceFilesFolderName; }
	}

	public static string PlayerSpecificPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + playerSpecificFolderName; }
	}
	
	public static string PitchFilesPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + pitchFilesName; }
	}
	
	public static string SessionFilesPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + sessionFilesName; }
	}

	public static string PlayerMeasuresPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + playermeasuresName; }
	}

	public static string UnfinishedSessionPath{
		get { return DropboxPath + delim + dashboardFolderName + delim + unfinishedSessionFolderName; }
	}

	// Write out exit code file to let know the dashboard if we exited well or not
	// Use code 1 for error, code 0 for everything ok.
	public static void WriteExitCode(string module, int code){
		NeuroLog.Debug("writing exit code " + code + " for module " + module);
		string fn = System.Environment.MachineName + "_" + module + "_exit_code.txt";
		using (StreamWriter sw = new StreamWriter(TraceFilesPath + delim + fn)) {
			sw.WriteLine(code);
		}
	}
	
	//Constructor for XmlManager
	public XmlManager(){
		gm = GameManager.main;
	}
	
	/*
	 * Checks to see if dir exists, and if not tries to create. If all goes
	 * well returns true. If something goes wrong, false is returned.
	 */
	private static bool CheckAndCreateDir(string dirname)
	{
		DirectoryInfo dir = new DirectoryInfo(dirname);
		//If it doesn't exist, try to create the folder
		if(!dir.Exists) {
			try {
				System.IO.Directory.CreateDirectory(dir.FullName);
			}
			//If it can't create the folder, return false
			catch (System.Exception e) {
				NeuroLog.Debug("Couldn't create directory " + dir.FullName + ": " + e);
				return false;
			}
		}
		return true;
	}

	/*
	 * Checks that all the folders we define here are accessible, and if not
	 * creates them. If something goes wrong returns False.
	 */
	public static bool CheckFolders()
	{
		if (!CheckAndCreateDir(DashboardPath)) return false;

		if (!CheckAndCreateDir(TraceFilesPath)) return false;

		if (!CheckAndCreateDir(LogFilesPath)) return false;
		
		if (!CheckAndCreateDir(PlayerSpecificPath)) return false;
		
		if (!CheckAndCreateDir(UnfinishedSessionPath)) return false;
		
		if (!CheckAndCreateDir(SessionFilesPath)) return false;
		
		if (!CheckAndCreateDir(PitchFilesPath)) return false;
		
		if(!CheckAndCreateDir(PlayerMeasuresPath)) return false;
		
		return true;
	}
	
	//Base method used to start the read in process, returns true if successful
	public List<EventStats> ReadInSession()
	{
		int tNum = PlayerPrefs.GetInt("-currentTask");
		
		//Check to see if the task number is in the correct range of 1-6
		if(tNum<1 ||tNum>6){
			
			NeuroLog.Error("Invalid task number given: " + tNum.ToString());
			
			sessionXML = "randomList";
			
			return null;
		}
		
		//Set the user and session file
		sessionXML = "task"+tNum.ToString();
		mUserName = PlayerPrefs.GetString("-player").Replace(" ","");
		
		//Add an xml extension
		string fn = sessionXML;
		if (!fn.EndsWith(".xml")) fn += ".xml";
		
		//Set up the XmlDocument variable
		XmlDocument xml = new XmlDocument();
		
		//Try to load the file. If it fails, exit out
		try{
			if(File.Exists(XmlManager.SessionFilesPath + XmlManager.delim + fn)){
				Debug.Log("Attempting to Load " + XmlManager.SessionFilesPath + XmlManager.delim + fn);
				xml.Load(XmlManager.SessionFilesPath + XmlManager.delim + fn);
			}
			else{
				NeuroLog.Error("Attempting to read from local Resources");
					
				TextAsset sessionData = Resources.Load("session_files/" + sessionXML) as TextAsset;
		
				TextReader reader = new StringReader(sessionData.text);
			
				xml.Load(reader);
			}
		}
		catch(Exception e){
			NeuroLog.Error("Unable to find sessionFile! Error: " + e);
			
			sessionXML = "randomList";
			
			return null;
		}
		
		XmlNode sessionNode = xml.SelectSingleNode("/session");
		
		string gameType = sessionNode.Attributes[0].Value;
		
		if((gameType !="0" && (typeof(SpatialManager) == gm.GetType())) ||
			(gameType !="1" && (typeof(InhibitionManager) == gm.GetType())) ||
			(gameType !="2" && (typeof(StarManager) == gm.GetType())) ){
			NeuroLog.Error("Invalid read in file for current scene. File is " + gameType+ " while scene is " + gm.GetType());
			
			sessionXML = "randomList";
			
			return null;
		}
		
		
		XmlNode eventsNode = xml.SelectSingleNode("/session/events");
		
		List<EventStats> eS;
		
		//Read in Trials
		if(typeof(SpatialManager) == gm.GetType())
			eS = ReadSpatialEvents(eventsNode);
		else if(typeof(InhibitionManager) == gm.GetType())
			eS = ReadInhibEvents(eventsNode);
		else if(typeof(StarManager) == gm.GetType())
			eS = ReadStarEvents(eventsNode);
		else
			return null;
		
		//Make sure it actually read in something
		if(eS == null || eS.Count == 0){
			NeuroLog.Error("No events found. Generating random set");
			sessionXML = "randomList";
			return null;
		}
		
		NeuroLog.Log("Finished Reading Trials");
		
		//Good Exit
		return eS;
	}
	
	//Method used to read in all the trials of the game
	private List<EventStats> ReadSpatialEvents(XmlNode eventsNode){
		
		List<EventStats> events = new List<EventStats>();
		
		//For all the trials
		for (int i=0; i<eventsNode.ChildNodes.Count; i++){
			//Make sure were trying to deal with trial
			if(eventsNode.ChildNodes[i].Name =="event"){
			
				List<int> dots = new List<int>();
				float delay=.1f;
				
				//For each attribute in the trial
				foreach(XmlAttribute attri in eventsNode.ChildNodes[i].Attributes){
					//StartRotation
					if(attri.Name.ToLower() == "dots"){
						int num;
			
						string[] d = attri.Value.Split(';');
						for(int j =0;j<d.Length;j++){
							if(int.TryParse(d[j],out num)){
								if(num<1 || num>9){
									Debug.LogError("Invalid Value for '"+ d[j] +"' value for 'dots' of trial #" + (j+1).ToString() + ". Needs to be between 1 and 9.");
								}
								else{
									dots.Add(num);
								}
							}
							else{
								Debug.LogError("Invalid ValueType for '"+ d[j] +"' value for 'dots' of trial #" + (j+1).ToString() + ". Needs to be a float.");
							}
						}
					}
					//Delay
					else if(attri.Name.ToLower() == "delay"){	
						if(float.TryParse(attri.Value,out delay)){
							if(delay!= .1f && delay != 3){
								NeuroLog.Error("Invalid value for 'delay' at trial #" + (i+1).ToString() + ". Needs to be either .1 or 3.");
								delay = .1f;
							}
						}
						else NeuroLog.Error("Invalid value for 'delay' at trial #" + (i+1).ToString() + ". Needs to be a float.");
					}
					//Other attributes that don't have cases
					else NeuroLog.Error("Unknown attribute '" + attri.Name + "' at trial #" + (i+1).ToString() + ".");
				}
				
				
				SpatialEvent sE = new SpatialEvent(dots);
				sE.Delay = delay;
				
				//Add the newly compiled event to the event list
				events.Add(sE);
			}
		}
		
		//Set the event list
		return events;
	}
	
	//Method used to read in all the trials of the game
	private List<EventStats> ReadInhibEvents(XmlNode eventsNode){
		
		List<EventStats> inhibEvents = new List<EventStats>();
		
		//For all the trials
		for (int i=0; i<eventsNode.ChildNodes.Count; i++){
			
			char charVar='0';
			string color="";
			
			//Make sure were trying to deal with event
			if(eventsNode.ChildNodes[i].Name =="event"){
				foreach(XmlAttribute attri in eventsNode.ChildNodes[i].Attributes){
					//Side
					if(attri.Name.ToLower() == "side"){	
						if(char.TryParse(attri.Value.ToLower(),out charVar)){
							if(charVar !='l'  && charVar !='r'){
								NeuroLog.Error("Invalid value for 'side' at event #" + (i+1).ToString() + ". Needs to be either 'l' or 'r'.");
								charVar = 'l';
							}
						}
						else 
							NeuroLog.Error("Invalid value for 'side' at event #" + (i+1).ToString() + ". Needs to be a char.");
					}
					//Color
					else if(attri.Name.ToLower() == "color"){	
						color = attri.Value.ToLower();
						if(color!="yellow" && color!="purple"){	
							NeuroLog.Error("Invalid value for 'color' at event #" + (i+1).ToString() + ". Needs to be either 'yellow' or 'purple'.");
							color = "yellow";
						}
					}
					//Other attributes that don't have cases
					else NeuroLog.Error("Unknown attribute '" + attri.Name + "' at event #" + (i+1).ToString() + ".");
				}
				
				InhibitionEvent iE = new InhibitionEvent(charVar,color);
				
				inhibEvents.Add(iE);
			}	
		}
		return inhibEvents;
	}
	
	//Method used to read in all the trials of the game
	private List<EventStats> ReadStarEvents(XmlNode eventsNode){
		
		List<EventStats> starEvents = new List<EventStats>();
		
		//For all the trials
		foreach(XmlNode trial in eventsNode.ChildNodes){
		
			StarEvent sE = new StarEvent();
			
			int lS=0;
			int bS=0;
			int d=0;
			int t=0;
			
			List<StarObject> objs = new List<StarObject>();
			
			for (int i=0; i<trial.ChildNodes.Count; i++){
			
				//Make sure were trying to deal with event
				if(trial.ChildNodes[i].Name =="event"){
					
					int type = -1;
					Vector2 pos = new Vector2(Mathf.Infinity,Mathf.Infinity);
					float rotation = 0;
					
					foreach(XmlAttribute attri in trial.ChildNodes[i].Attributes){
						//Type
						if(attri.Name.ToLower() == "type"){	
							if(int.TryParse(attri.Value.ToLower(),out type)){
								if(type <0  && type>3){
									NeuroLog.Error("Invalid value for 'type' at event #" + (i+1).ToString() + ". Needs to be between 0 and 4.");
									type = -1;
								}
								else{
									if(type == 0)
										lS++;
									else if(type ==1)
										bS++;
									else if(type ==2)
										d++;
									else if(type ==3)
										t++;
								}
							}
							else NeuroLog.Error("Invalid value for 'type' at event #" + (i+1).ToString() + ". Needs to be a int.");
						}
						else if(attri.Name.ToLower() =="position"){
							string[] input = attri.Value.Split(',');
							
							float x = Mathf.Infinity;
							float y = Mathf.Infinity;
							
							if(float.TryParse(input[0],out x)){
								if(x!=Mathf.Infinity){
									if(x>170 || x<-170){
										x = Mathf.Infinity;
										NeuroLog.Error("Invalid value for 'position.x' at event #" + (i+1).ToString() + ". Needs to be between -170 and 170.");
									}
								}
							}
							else NeuroLog.Error("Invalid value for 'position.x' at event #" + (i+1).ToString() + ". Needs to be a float.");
							
							if(float.TryParse(input[1],out y)){
								if(y!=Mathf.Infinity){
									if(y>90 || y<-75){
										y = Mathf.Infinity;
										NeuroLog.Error("Invalid value for 'position.y' at event #" + (i+1).ToString() + ". Needs to be between -75 and 90.");
									}
								}
							}
							else NeuroLog.Error("Invalid value for 'position.y' at event #" + (i+1).ToString() + ". Needs to be a float.");
							
							pos = new Vector2(x,y);
						}
						//Color
						else if(attri.Name.ToLower() == "rotation"){	
							if(!float.TryParse(attri.Value.ToLower(),out rotation))
								NeuroLog.Error("Invalid value for 'rotation' at event #" + (i+1).ToString() + ". Needs to be a float.");
						}
						//Other attributes that don't have cases
						else NeuroLog.Error("Unknown attribute '" + attri.Name + "' at event #" + (i+1).ToString() + ".");
					}
					
					if(type!=-1 && pos.x != Mathf.Infinity && pos.y != Mathf.Infinity){
						StarObject sO = new StarObject(type,pos,rotation);
						
						objs.Add(sO);
					}
				}	
			}
			
			sE.Objects = objs;
			
			sE.NumBigStars = bS;
			sE.NumLittleStars=lS;
			sE.NumTriangles = t;
			sE.NumDots = d;
			
			starEvents.Add(sE);
		}
		return starEvents;
	}
	
	//Method used to write out the log file once the game has completed
	public void WriteOut(){
		
		//Generate the file name
		GenerateNewTimestamp();
		
		//Set up the xml document
		XmlDocument xml = new XmlDocument();
		
		//Create declaration
		XmlDeclaration dec = xml.CreateXmlDeclaration("1.0", "utf-8",null);
		xml.AppendChild(dec);
		
		// Create the root element "session"
		XmlElement session = xml.CreateElement("session");
		xml.InsertBefore(dec, xml.DocumentElement);
		xml.AppendChild(session);
		
		if(gm.SType == GameManager.SessionType.Spatial) WriteOutSpatial(xml,session);
		else if(gm.SType == GameManager.SessionType.Inhibition) WriteOutInhibition(xml,session);
		else if (gm.SType == GameManager.SessionType.Star) WriteOutStar(xml,session);
		
		//Save the file in the correct spot
		xml.Save(LogFilesPath+delim+statsXML);
	}
	
	private void WriteOutSpatial(XmlDocument xml, XmlElement session){
		XmlElement dots = xml.CreateElement("dots");
		
		for(float i =1; i<10;i++){
			XmlElement dot = xml.CreateElement("Dot");
			
			dot.SetAttribute("Number", i.ToString());
			
			Vector2 dotPos = Vector2.zero;
			
			if(i%3 == 0) dotPos.x = Screen.width*(5f/6f);
			else if((i+1)%3==0) dotPos.x = Screen.width*(3f/6f);
			else dotPos.x = Screen.width/6f;
		
			if(i/3f>2)dotPos.y = Screen.height*(5f/6f);
			else if(i/3f>1) dotPos.y = Screen.height*(3f/6f);
			else dotPos.y = Screen.height/6f;
			
			dot.SetAttribute("Center", dotPos.ToString());
			
			dots.AppendChild(dot);
		}
		
		session.AppendChild(dots);
	
		XmlElement practice = xml.CreateElement("practice");
		
		//Loop through all the events and write them out
		foreach(SpatialEvent eS in gm.Practice){
			XmlElement trial = xml.CreateElement("trial");
			
				trial.SetAttribute("Dot1", eS.Dots[0].ToString());
				if(eS.Dots.Count>1)
					trial.SetAttribute("Dot2", eS.Dots[1].ToString());
				if(eS.Dots.Count>2)
					trial.SetAttribute("Dot3", eS.Dots[2].ToString());
			
				trial.SetAttribute("Delay", eS.Delay.ToString());
			
				trial.SetAttribute("TimedOut", eS.TimedOut.ToString());
			
				foreach(Response r in eS.BadResponses){
					XmlElement falseresponse = xml.CreateElement("falseresponse");
					falseresponse.SetAttribute("DotPressed",r.DotPressed.ToString());
					falseresponse.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					falseresponse.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					falseresponse.SetAttribute("DistanceFromCenter",r.DistanceFromCenter.ToString());
					trial.AppendChild(falseresponse);
				}
			
				foreach(Response r in eS.Responses){
					XmlElement response = xml.CreateElement("response");
					response.SetAttribute("DotPressed",r.DotPressed.ToString());
					response.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					response.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					response.SetAttribute("DistanceFromCenter",r.DistanceFromCenter.ToString());
					trial.AppendChild(response);
				}
			practice.AppendChild(trial);
		}
		
		session.AppendChild(practice);
		
		//Create the trial cluster
		XmlElement trials = xml.CreateElement("trials");
		
		//Loop through all the events and write them out
		foreach(SpatialEvent eS in gm.Events){
			XmlElement trial = xml.CreateElement("trial");
			
				trial.SetAttribute("Dot1", eS.Dots[0].ToString());
				if(eS.Dots.Count>1)
					trial.SetAttribute("Dot2", eS.Dots[1].ToString());
				if(eS.Dots.Count>2)
					trial.SetAttribute("Dot3", eS.Dots[2].ToString());
			
				trial.SetAttribute("Delay", eS.Delay.ToString());
			
				trial.SetAttribute("TimedOut", eS.TimedOut.ToString());
			
				foreach(Response r in eS.BadResponses){
					XmlElement falseresponse = xml.CreateElement("falseresponse");
					falseresponse.SetAttribute("DotPressed",r.DotPressed.ToString());
					falseresponse.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					falseresponse.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					falseresponse.SetAttribute("DistanceFromCenter",r.DistanceFromCenter.ToString());
					trial.AppendChild(falseresponse);
				}
			
				foreach(Response r in eS.Responses){
					XmlElement response = xml.CreateElement("response");
					response.SetAttribute("DotPressed",r.DotPressed.ToString());
					response.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					response.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					response.SetAttribute("DistanceFromCenter",r.DistanceFromCenter.ToString());
					trial.AppendChild(response);
				}
			trials.AppendChild(trial);
		}
	
		session.AppendChild(trials);
	}
	
	private void WriteOutInhibition(XmlDocument xml, XmlElement session){
		XmlElement dots = xml.CreateElement("dots");
		
		XmlElement ldot = xml.CreateElement("Left");
			
		Vector2 dotPos = new Vector2(Screen.width/4f,Screen.height/2f);
			
		ldot.SetAttribute("Center", dotPos.ToString());
		
		dots.AppendChild(ldot);
		
		XmlElement rdot = xml.CreateElement("Right");
			
		dotPos = new Vector2(Screen.width*.75f,Screen.height/2f);
			
		rdot.SetAttribute("Center", dotPos.ToString());
			
		dots.AppendChild(rdot);
		session.AppendChild(dots);
		XmlElement practice = xml.CreateElement("practice");
		
		//Loop through all the events and write them out
		foreach(InhibitionEvent eS in gm.Practice){
			XmlElement trial = xml.CreateElement("trial");
				
				if(eS.DotColor =="yellow")
					trial.SetAttribute("TargetSide", "same");
				else
					trial.SetAttribute("TargetSide", "opposite");
			
				trial.SetAttribute("TimedOut", eS.TimedOut.ToString());
				
				if(eS.PlayerResponse!=null){
					trial.SetAttribute("ResponseTime", eS.PlayerResponse.ResponseTime.ToString());
					trial.SetAttribute("TouchPosition", eS.PlayerResponse.TouchLocation.ToString());
					trial.SetAttribute("DistanceFromCenter",eS.PlayerResponse.DistanceFromCenter.ToString());
			
					if(eS.PlayerResponse.DotPressed ==1)
						trial.SetAttribute("PressedSide","right");
					else
						trial.SetAttribute("PressedSide","left");
				}
				
				if(eS.respondedCorrectly())
					trial.SetAttribute("Correct","1");
				else
					trial.SetAttribute("Correct","0");
			practice.AppendChild(trial);
		}
		
		session.AppendChild(practice);
		
		//Create the trial cluster
		XmlElement trials = xml.CreateElement("trials");
		
		//Loop through all the events and write them out
		foreach(InhibitionEvent eS in gm.Events){
			XmlElement trial = xml.CreateElement("trial");
			
				if(eS.DotColor =="yellow") trial.SetAttribute("TargetSide", "same");
				else trial.SetAttribute("TargetSide", "opposite");
			
				trial.SetAttribute("TimedOut", eS.TimedOut.ToString());
				
				if(eS.PlayerResponse!=null){
					trial.SetAttribute("ResponseTime", eS.PlayerResponse.ResponseTime.ToString());
					trial.SetAttribute("TouchPosition", eS.PlayerResponse.TouchLocation.ToString());
					trial.SetAttribute("DistanceFromCenter",eS.PlayerResponse.DistanceFromCenter.ToString());
				
					if(eS.PlayerResponse.DotPressed ==1) trial.SetAttribute("PressedSide","right");
					else trial.SetAttribute("PressedSide","left");
				}
				
				if(eS.respondedCorrectly()) trial.SetAttribute("Correct","1");
				else trial.SetAttribute("Correct","0");
			
			trials.AppendChild(trial);
		}
		session.AppendChild(trials);	
	}
	
	private void WriteOutStar(XmlDocument xml, XmlElement session){

		XmlElement practice = xml.CreateElement("practice");
		
		//Loop through all the events and write them out
		
		foreach(StarEvent eS in gm.Practice){
			XmlElement trial = xml.CreateElement("trial");
				
				trial.SetAttribute("EndCondition", eS.EndCondition);
				trial.SetAttribute("Duration",eS.Duration.ToString());
			
				trial.SetAttribute("NumGoodTouches", eS.NumGoodTouches.ToString());
				trial.SetAttribute("NumBadTouches", eS.NumBadTouches.ToString());
				trial.SetAttribute("NumRepeats", eS.RepeatTouches.ToString());
			
				trial.SetAttribute("AvgTimePerTarget", eS.AvgTimePerTarget().ToString());
				trial.SetAttribute("StandardDeviation", eS.StD().ToString());
				trial.SetAttribute("AvgTimePerAction", eS.AvgTimePerAction().ToString());
			
				List<int> perArea = eS.correctPerArea();
				string pArea="(";
				for(int i =0;i<perArea.Count;i++){
					pArea += perArea[i];
					if(i<perArea.Count-1) pArea+=",";
					else pArea+=")";
				}
			
				trial.SetAttribute("TargetsPerArea", pArea);
				trial.SetAttribute("AvgLocation", eS.AvgDistanceofTargets().ToString());
			
			
				trial.SetAttribute("AvgFirstTen", eS.AvgTimeStart().ToString());
				trial.SetAttribute("AvgLastTen", eS.AvgTimeLast().ToString());
				trial.SetAttribute("AvgDistancePerTarget", eS.AvgDistance().ToString());
			
				foreach(Response r in eS.Responses){
					XmlElement response = xml.CreateElement("response");
					response.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					response.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					trial.AppendChild(response);
				}
			practice.AppendChild(trial);
		}
		
		session.AppendChild(practice);
		
		
		//Create the trial cluster
		XmlElement trials = xml.CreateElement("trials");
		
		float avggood=0;
		float avgbad=0;
		float avgrepeat=0;
		float avgtimetarget=0;
		float avgtimeaction=0;
		float avgstd=0;
		List<float> avgperArea=new List<float>(){0,0,0,0,0,0,0,0};
		float avglocation=0;
		float avgfirst=0;
		float avglast=0;
		float avgdist=0;
		
		//Loop through all the events and write them out
		foreach(StarEvent eS in gm.Events){
			XmlElement trial = xml.CreateElement("trial");
			
				trial.SetAttribute("EndCondition", eS.EndCondition);
			
				trial.SetAttribute("Duration",eS.Duration.ToString());
			
				float good=eS.NumGoodTouches;
				avggood +=good;
				float bad=eS.NumBadTouches;
				avgbad+=bad; 
				float repeat=eS.RepeatTouches;
				avgrepeat +=repeat;
				float timetarget=eS.AvgTimePerTarget();
				avgtimetarget += timetarget;
				float timeaction=eS.AvgTimePerAction();
				avgtimeaction += timeaction;
				float std=eS.StD();
				avgstd+= std;
				List<int> perArea= eS.correctPerArea();
				string pArea="(";
				for(int i =0;i<perArea.Count;i++){
					avgperArea[i] += perArea[i];
				
					pArea += perArea[i];
					if(i<perArea.Count-1)
						pArea+=",";
					else
						pArea+=")";
				}
				float location=eS.AvgDistanceofTargets();
				avglocation+= location;
				float first=eS.AvgTimeStart();
				avgfirst+= first;
				float last=eS.AvgTimeLast();
				avglast+= last;
				float dist=eS.AvgDistance();
				avgdist+= dist;
			
				trial.SetAttribute("NumGoodTouches", good.ToString());
				trial.SetAttribute("NumBadTouches", bad.ToString());
				trial.SetAttribute("NumRepeats", repeat.ToString());
			
				trial.SetAttribute("AvgTimePerTarget", timetarget.ToString());
				trial.SetAttribute("StandardDeviation", std.ToString());
				trial.SetAttribute("AvgTimePerAction", timeaction.ToString());
			
			
				trial.SetAttribute("TargetsPerArea",pArea);
				trial.SetAttribute("AvgLocation", location.ToString());
			
			
				trial.SetAttribute("AvgFirstTen", first.ToString());
				trial.SetAttribute("AvgLastTen", last.ToString());
				trial.SetAttribute("AvgDistancePerTarget", dist.ToString());
			
				foreach(Response r in eS.Responses){
					XmlElement response = xml.CreateElement("response");
				
					string t ="";
					if(r.ResponseType==0)
						t="GOOD";
					else if(r.ResponseType==1)
						t="BAD";
					else if(r.ResponseType==2)
						t="REPEAT";
				
					response.SetAttribute("ResponseType", t);
					response.SetAttribute("ResponseTime", r.ResponseTime.ToString());
					response.SetAttribute("TouchPosition", r.TouchLocation.ToString());
					trial.AppendChild(response);
				}
			
			trials.AppendChild(trial);
		}
		avggood/=gm.Events.Count;
		avgbad/=gm.Events.Count;
		avgrepeat/=gm.Events.Count;
		avgtimetarget/=gm.Events.Count;
		avgtimeaction/=gm.Events.Count;
		avgstd/=gm.Events.Count;
		string avgArea="(";
		for(int i =0;i<avgperArea.Count;i++){
			avgperArea[i] /= gm.Events.Count;
			
			avgArea += avgperArea[i];
			if(i<avgperArea.Count-1)
				avgArea+=",";
			else
				avgArea+=")";
		}
		avglocation/=gm.Events.Count;
		avgfirst/=gm.Events.Count;
		avglast/=gm.Events.Count;
		avgdist/=gm.Events.Count;
		
		
		trials.SetAttribute("AvgGoodTouches", avggood.ToString());
		trials.SetAttribute("AvgBadTouches",avgbad.ToString());
		trials.SetAttribute("AvgRepeats", avgrepeat.ToString());
	
		trials.SetAttribute("AvgTimePerTarget", avgtimetarget.ToString());
		trials.SetAttribute("AvgStandardDeviation", avgstd.ToString());
		trials.SetAttribute("AvgTimePerAction", avgtimeaction.ToString());
	
		trials.SetAttribute("AvgTargetsPerArea",avgArea);
		trials.SetAttribute("AvgLocation", avglocation.ToString());
	
		trials.SetAttribute("AvgFirstTen", avgfirst.ToString());
		trials.SetAttribute("AvgLastTen", avglast.ToString());
		trials.SetAttribute("AvgDistancePerTarget", avgdist.ToString());
	
		session.AppendChild(trials);	
	}
	
	//Generates the time the file was written out, and writeout file name
	public void GenerateNewTimestamp(){
		DateTime Now = DateTime.Now;
		string theTime = Now.ToShortDateString().Replace('/', '-') + "-" + Now.ToString("HH:mm:ss").Replace(':', '-');
		
		string[] filePaths = Directory.GetFiles(XmlManager.LogFilesPath);
		
		int count =1;
		
		foreach(string s in filePaths){
			if(s.Contains((mUserName+"_"+sessionXML))) count++;
		}
		
		try{
			//Regex noXML = new Regex(".xml", RegexOptions.IgnoreCase);
			statsXML = mUserName + "_"+sessionXML+"_" + theTime;
			
			if(count>1 && sessionXML != "randomList")	statsXML += ("_try" + count.ToString());
			
			statsXML+=".xml";
		} catch(ArgumentOutOfRangeException e) {
			// If the player quits the game before a gameType is chosen
			NeuroLog.Debug(e.Message);
			statsXML = mUserName +"_None_" + theTime + ".xml";
		}
	}
}