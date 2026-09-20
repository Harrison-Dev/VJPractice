ObjC.import('Foundation');
const spotify=Application('com.spotify.client');
function emit(data){$.NSFileHandle.fileHandleWithStandardOutput.writeData($(JSON.stringify(data)+'\n').dataUsingEncoding($.NSUTF8StringEncoding));}
while(true){try{if(!spotify.running()){emit({connected:false,error:'請開啟 Spotify'});}else{const state=spotify.playerState();if(state==='stopped'){emit({connected:false,error:'請先播放一首歌'});}else{const t=spotify.currentTrack();emit({connected:true,playing:state==='playing',position:spotify.playerPosition(),duration:t.duration()/1000,title:t.name(),artist:t.artist(),trackId:t.id()});}}}catch(e){emit({connected:false,error:String(e)});}delay(0.35);}
