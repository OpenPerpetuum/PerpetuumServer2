/****** Object:  StoredProcedure [dbo].[changeFacilities]    Script Date: 10.05.2026 13:22:48 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[changeFacilities] 
	@baseName VARCHAR(50),
	@facilityLevels VARCHAR(max)
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @baseEid BIGINT
	SET @baseEid = (SELECT eid FROM dbo.entities WHERE ename=@baseName)

	IF (@baseEid IS NULL)
	BEGIN
		SELECT 'ERROR!! no eid for name: ' , @baseName
		RETURN
	end

	DECLARE @facilityEid BIGINT, @newDef INT, @pattern VARCHAR(50)
	
	DECLARE @id int, @value VARCHAR(max)
	DECLARE levels CURSOR LOCAL STATIC FORWARD_ONLY FOR SELECT id,value FROM dbo.splitString(@facilityLevels,',')
	OPEN levels
	FETCH NEXT FROM levels INTO @id,@value
	WHILE @@FETCH_STATUS = 0
	BEGIN
		
		SET @facilityEid = NULL;
		SET @newDef = NULL;
		
		IF (@value IS NULL)
		BEGIN
			SELECT 'ERROR!!! level is null.'
		    RETURN
		END


		IF (@id=1)
		BEGIN
			SET @pattern = 'refinery';
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
		end
		ELSE IF (@id=2)
		BEGIN
			SET @pattern = 'research';
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
		end
		ELSE IF (@id=3)
		BEGIN
			SET @pattern = 'reprocessor';
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
		end
		ELSE IF (@id=4)
		BEGIN
			SET @pattern = 'repair';
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
		end
		ELSE IF (@id=5)
		BEGIN
			SET @pattern = 'mill';
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
		end
		ELSE IF (@id=6)
		BEGIN
			SET @pattern = 'prototyper';
			SET @newDef = dbo.productionFacilityByLevel(@pattern,@value)
			SET @facilityEid = dbo.productionFacilityFromBaseByPattern(@baseEid,@pattern);
		end

		IF (@facilityEid IS NULL)
		BEGIN
			SELECT 'ERROR!! no facility', @pattern, 'base', @baseEid
		END

		IF (@newDef IS NULL)
		BEGIN
			SELECT 'ERROR! no new definition', @pattern
		END

		IF (@newDef IS NOT NULL AND @facilityEid IS NOT NULL)
		BEGIN
		
		--display
		SELECT eid, dbo.GetDefinitionName([definition]), '=>' , dbo.GetDefinitionName(@newDef) FROM dbo.entities WHERE eid=@facilityEid

		-- the actual work
		--UPDATE dbo.entities SET [definition]=@newDef WHERE eid=@facilityEid;

		    
		END

		
		FETCH NEXT FROM levels INTO @id,@value
	END
    CLOSE levels;DEALLOCATE levels


	SELECT ' '
END
GO