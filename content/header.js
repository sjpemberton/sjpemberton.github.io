window.onscroll = function(e) {
	var header = document.getElementById('header');
	var content = document.getElementById('page-content');
	if (this.scrollY > 0){  
		if(header.className.indexOf('sticky') == -1){
	     	content.className = header.className = header.className + " sticky";
			}
	  }
	  else{
	    content.className = header.className = header.className.replace(' sticky','')
	  }
};