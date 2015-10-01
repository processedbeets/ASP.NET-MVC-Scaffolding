set OutputPath=%1
set ViewsNeedingCode=(RazorView\_CreateOrEdit, RazorView\Create, RazorView\Delete, RazorView\Details, RazorView\Edit, RazorView\Index, AspxView\CreateOrEdit, AspxView\Create, AspxView\Delete, AspxView\Details, AspxView\Edit, AspxView\Index)

for %%x in %ViewsNeedingCode% do (
	type Scaffolders\Views\ViewTemplateCode.cs.t4 >> %OutputPath%\Scaffolders\%%x.cs.t4;
	type Scaffolders\Views\ViewTemplateCode.vb.t4 >> %OutputPath%\Scaffolders\%%x.vb.t4;
)